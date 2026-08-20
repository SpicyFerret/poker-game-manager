using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Rankings;

public sealed record RankingRow
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; }

    /// <summary>Place in this ranking, counting from 1. Differs between the two.</summary>
    public int Position { get; init; }

    public int Points { get; init; }
    public decimal Balance { get; init; }
    public int TablesPlayed { get; init; }

    /// <summary>Nights finished first. Not what either ranking sorts by, but the first thing people ask.</summary>
    public int Wins { get; init; }

    public int BestPosition { get; init; }
}

/// <summary>
/// The two rankings the group asked for, over the whole championship — which is
/// the season, so there is no window to choose.
/// </summary>
public sealed record RankingsResponse
{
    /// <summary>By the championship's points table.</summary>
    public IReadOnlyList<RankingRow> ByPoints { get; init; } = [];

    /// <summary>By money won and lost. The fairer of the two, and the harder one to keep on paper.</summary>
    public IReadOnlyList<RankingRow> ByBalance { get; init; } = [];

    public int TablesCounted { get; init; }
}

public sealed record GetRankingsQuery(Guid ChampionshipId) : IQuery<RankingsResponse>;

internal sealed class GetRankingsQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetRankingsQuery, RankingsResponse>
{
    public async Task<Result<RankingsResponse>> Handle(
        GetRankingsQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<RankingsResponse>(caller.Error);
        }

        IQueryable<PokerTable> finished = FinishedTables.For(context, query.ChampionshipId);

        // Every ranking is a sum over TableResult, never a recomputation from the
        // ledger: the night's numbers were frozen when it settled, and people have
        // already paid each other on the strength of them.
        var rows = await context.TableResults
            .Join(finished, result => result.TableId, table => table.Id, (result, _) => result)
            .Join(
                context.TablePlayers,
                result => result.TablePlayerId,
                player => player.Id,
                (result, player) => new { player.UserId, player.User.DisplayName, result })
            .GroupBy(row => new { row.UserId, row.DisplayName })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.DisplayName,
                Points = group.Sum(row => row.result.Points),
                Balance = group.Sum(row => row.result.Balance),
                TablesPlayed = group.Count(),
                Wins = group.Count(row => row.result.Position == 1),
                BestPosition = group.Min(row => row.result.Position)
            })
            .ToListAsync(cancellationToken);

        int tablesCounted = await finished.CountAsync(cancellationToken);

        return new RankingsResponse
        {
            // Ties broken by the other ranking's measure, then by name so the
            // order never wobbles between two requests that are otherwise level.
            ByPoints =
            [
                .. rows
                    .OrderByDescending(row => row.Points)
                    .ThenByDescending(row => row.Balance)
                    .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
                    .Select((row, index) => new RankingRow
                    {
                        UserId = row.UserId,
                        DisplayName = row.DisplayName,
                        Position = index + 1,
                        Points = row.Points,
                        Balance = row.Balance,
                        TablesPlayed = row.TablesPlayed,
                        Wins = row.Wins,
                        BestPosition = row.BestPosition
                    })
            ],
            ByBalance =
            [
                .. rows
                    .OrderByDescending(row => row.Balance)
                    .ThenByDescending(row => row.Points)
                    .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
                    .Select((row, index) => new RankingRow
                    {
                        UserId = row.UserId,
                        DisplayName = row.DisplayName,
                        Position = index + 1,
                        Points = row.Points,
                        Balance = row.Balance,
                        TablesPlayed = row.TablesPlayed,
                        Wins = row.Wins,
                        BestPosition = row.BestPosition
                    })
            ],
            TablesCounted = tablesCounted
        };
    }
}
