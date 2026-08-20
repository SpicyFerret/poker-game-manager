using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Rankings;

public sealed record NightRecord
{
    public string DisplayName { get; init; }
    public string TableName { get; init; }
    public decimal Balance { get; init; }
}

public sealed record StatisticsResponse
{
    public int TablesPlayed { get; init; }
    public int DistinctPlayers { get; init; }

    /// <summary>Everything paid in across every finished table: buy-ins and rebuys.</summary>
    public decimal MoneyIn { get; init; }

    public int Rebuys { get; init; }
    public decimal AverageMoneyPerTable { get; init; }

    /// <summary>The best and worst single nights anyone has had. Null before anything has been settled.</summary>
    public NightRecord? BiggestWin { get; init; }
    public NightRecord? BiggestLoss { get; init; }
}

public sealed record GetStatisticsQuery(Guid ChampionshipId) : IQuery<StatisticsResponse>;

internal sealed class GetStatisticsQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetStatisticsQuery, StatisticsResponse>
{
    public async Task<Result<StatisticsResponse>> Handle(
        GetStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<StatisticsResponse>(caller.Error);
        }

        IQueryable<PokerTable> finished = FinishedTables.For(context, query.ChampionshipId);

        int tablesPlayed = await finished.CountAsync(cancellationToken);

        List<Night> nights = await context.TableResults
            .Join(finished, result => result.TableId, table => table.Id, (result, table) => new { result, table })
            .Join(
                context.TablePlayers,
                row => row.result.TablePlayerId,
                player => player.Id,
                (row, player) => new Night(
                    player.UserId,
                    player.User.DisplayName,
                    row.table.Name,
                    row.result.Balance))
            .ToListAsync(cancellationToken);

        var ledger = await context.LedgerEntries
            .Join(finished, entry => entry.TableId, table => table.Id, (entry, _) => entry)
            .Where(entry => entry.Type == LedgerEntryType.BuyIn || entry.Type == LedgerEntryType.Rebuy)
            .Select(entry => new { entry.Type, entry.MoneyAmount })
            .ToListAsync(cancellationToken);

        decimal moneyIn = ledger.Sum(entry => entry.MoneyAmount);

        return new StatisticsResponse
        {
            TablesPlayed = tablesPlayed,
            DistinctPlayers = nights.Select(night => night.UserId).Distinct().Count(),
            MoneyIn = moneyIn,
            Rebuys = ledger.Count(entry => entry.Type == LedgerEntryType.Rebuy),
            AverageMoneyPerTable = tablesPlayed == 0
                ? 0m
                : decimal.Round(moneyIn / tablesPlayed, 2, MidpointRounding.AwayFromZero),
            BiggestWin = ToRecord(nights.OrderByDescending(night => night.Balance).FirstOrDefault()),
            BiggestLoss = ToRecord(nights.OrderBy(night => night.Balance).FirstOrDefault())
        };
    }

    /// <summary>One player's result at one table, flattened for the aggregates above.</summary>
    private sealed record Night(Guid UserId, string DisplayName, string TableName, decimal Balance);

    private static NightRecord? ToRecord(Night? night) => night is null
        ? null
        : new NightRecord
        {
            DisplayName = night.DisplayName,
            TableName = night.TableName,
            Balance = night.Balance
        };
}
