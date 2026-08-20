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

    /// <summary>
    /// What the winner took home: chips counted, less everything they paid in.
    /// Negative is possible on a night where everyone lost to the one person who
    /// left early. Note this is balance, not chips held — someone several rebuys
    /// deep can end holding the biggest pile and still be down on the night.
    /// </summary>
    public decimal WinnerBalance { get; init; }

    /// <summary>Everything paid in across the table: buy-ins and rebuys.</summary>
    public decimal MoneyIn { get; init; }
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

                return new HistoryRow
                {
                    TableId = table.Id,
                    Name = table.Name,
                    ClosedAtUtc = table.ClosedAtUtc,
                    PlayerCount = playerCounts.GetValueOrDefault(table.Id),
                    WinnerDisplayName = winner?.DisplayName,
                    WinnerBalance = winner?.Balance ?? 0m,
                    MoneyIn = moneyIn.GetValueOrDefault(table.Id)
                };
            })
            .ToList();
    }
}
