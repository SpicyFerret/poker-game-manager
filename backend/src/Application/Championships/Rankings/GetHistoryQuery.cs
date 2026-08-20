using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Rankings;

public sealed record HistoryRow
{
    public Guid TableId { get; init; }
    public string Name { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public int PlayerCount { get; init; }
    public string? WinnerDisplayName { get; init; }

    /// <summary>What the winner took home. Negative is possible on a bad night for everyone.</summary>
    public decimal WinnerBalance { get; init; }

    /// <summary>Everything paid in across the table: buy-ins and rebuys.</summary>
    public decimal MoneyIn { get; init; }

    /// <summary>
    /// Who finished holding the most chips.
    ///
    /// Not always the winner: balance is chips minus what you paid in, so
    /// somebody three rebuys deep can end the night with the biggest stack in
    /// front of them and still be down on the night. Both are worth showing.
    /// </summary>
    public string? ChipLeaderDisplayName { get; init; }

    /// <summary>What that stack was worth in money.</summary>
    public decimal ChipLeaderChips { get; init; }
}

public sealed record GetHistoryQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<HistoryRow>>;

internal sealed class GetHistoryQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetHistoryQuery, IReadOnlyList<HistoryRow>>
{
    public async Task<Result<IReadOnlyList<HistoryRow>>> Handle(
        GetHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HistoryRow>>(caller.Error);
        }

        List<PokerTable> tables = await FinishedTables
            .For(context, query.ChampionshipId)
            .OrderByDescending(table => table.ClosedAtUtc)
            .ToListAsync(cancellationToken);

        List<Guid> tableIds = [.. tables.Select(table => table.Id)];

        // One pass for the winners rather than a query per table: a championship
        // that has been running a year is a long list, and this screen is the one
        // people scroll.
        var winners = await context.TableResults
            .Where(result => tableIds.Contains(result.TableId) && result.Position == 1)
            .Join(
                context.TablePlayers,
                result => result.TablePlayerId,
                player => player.Id,
                (result, player) => new { result.TableId, player.User.DisplayName, result.Balance })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> playerCounts = await context.TablePlayers
            .Where(player => tableIds.Contains(player.TableId))
            .GroupBy(player => player.TableId)
            .Select(group => new { TableId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.TableId, row => row.Count, cancellationToken);

        // Chips held at the end, per player, valued at the table's own rate. Read
        // from the counts rather than from the result, because the result records
        // balance and balance has the buy-ins already taken off it.
        var stacks = await context.FinalCounts
            .Where(count => tableIds.Contains(count.TableId))
            .Join(
                context.ChipDenominations,
                count => count.ChipDenominationId,
                denomination => denomination.Id,
                (count, denomination) => new
                {
                    count.TableId,
                    count.TablePlayerId,
                    Units = count.Quantity * denomination.EffectiveValue
                })
            .GroupBy(row => new { row.TableId, row.TablePlayerId })
            .Select(group => new
            {
                group.Key.TableId,
                group.Key.TablePlayerId,
                Units = group.Sum(row => row.Units)
            })
            .Join(
                context.TablePlayers,
                stack => stack.TablePlayerId,
                player => player.Id,
                (stack, player) => new { stack.TableId, player.User.DisplayName, stack.Units })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, decimal> moneyIn = await context.LedgerEntries
            .Where(entry =>
                tableIds.Contains(entry.TableId) &&
                (entry.Type == LedgerEntryType.BuyIn || entry.Type == LedgerEntryType.Rebuy))
            .GroupBy(entry => entry.TableId)
            .Select(group => new { TableId = group.Key, Total = group.Sum(e => e.MoneyAmount) })
            .ToDictionaryAsync(row => row.TableId, row => row.Total, cancellationToken);

        return tables
            .Select(table =>
            {
                var winner = winners.SingleOrDefault(w => w.TableId == table.Id);

                var chipLeader = stacks
                    .Where(stack => stack.TableId == table.Id)
                    .OrderByDescending(stack => stack.Units)
                    .FirstOrDefault();

                return new HistoryRow
                {
                    TableId = table.Id,
                    Name = table.Name,
                    ClosedAtUtc = table.ClosedAtUtc,
                    PlayerCount = playerCounts.GetValueOrDefault(table.Id),
                    WinnerDisplayName = winner?.DisplayName,
                    WinnerBalance = winner?.Balance ?? 0m,
                    MoneyIn = moneyIn.GetValueOrDefault(table.Id),
                    ChipLeaderDisplayName = chipLeader?.DisplayName,
                    ChipLeaderChips = (chipLeader?.Units ?? 0) * table.MoneyPerUnit
                };
            })
            .ToList();
    }
}
