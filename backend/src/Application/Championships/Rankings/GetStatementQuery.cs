using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Tables;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Rankings;

public sealed record StatementRow
{
    public Guid TableId { get; init; }
    public string TableName { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public int Position { get; init; }
    public int Points { get; init; }
    public decimal Balance { get; init; }

    /// <summary>Buy-in, rebuys and chips bought, less anything credited for chips sold.</summary>
    public decimal PaidIn { get; init; }

    public int Rebuys { get; init; }
}

public sealed record StatementResponse
{
    public IReadOnlyList<StatementRow> Rows { get; init; } = [];
    public decimal TotalBalance { get; init; }
    public decimal TotalPaidIn { get; init; }
    public int TotalPoints { get; init; }
    public int Wins { get; init; }
}

/// <summary>
/// One player's own history in a championship, night by night. Answers the
/// question a ranking cannot: not where you stand, but where the money went.
/// </summary>
public sealed record GetStatementQuery(Guid ChampionshipId) : IQuery<StatementResponse>;

internal sealed class GetStatementQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext)
    : IQueryHandler<GetStatementQuery, StatementResponse>
{
    public async Task<Result<StatementResponse>> Handle(
        GetStatementQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<StatementResponse>(caller.Error);
        }

        Guid userId = userContext.UserId;

        IQueryable<PokerTable> finished = FinishedTables.For(context, query.ChampionshipId);

        var rows = await context.TableResults
            .Join(finished, result => result.TableId, table => table.Id, (result, table) => new { result, table })
            .Join(
                context.TablePlayers.Where(player => player.UserId == userId),
                row => row.result.TablePlayerId,
                player => player.Id,
                (row, player) => new
                {
                    row.table.Id,
                    row.table.Name,
                    row.table.ClosedAtUtc,
                    row.result.Position,
                    row.result.Points,
                    row.result.Balance,
                    PlayerId = player.Id
                })
            .ToListAsync(cancellationToken);

        List<Guid> playerIds = [.. rows.Select(row => row.PlayerId)];

        var ledger = await context.LedgerEntries
            .Where(entry => playerIds.Contains(entry.TablePlayerId))
            .Select(entry => new { entry.TablePlayerId, entry.Type, entry.MoneyAmount })
            .ToListAsync(cancellationToken);

        ILookup<Guid, decimal> paid = ledger
            .ToLookup(
                entry => entry.TablePlayerId,
                entry => LedgerMath.SignedPaidIn(entry.Type, entry.MoneyAmount));

        ILookup<Guid, int> rebuys = ledger
            .Where(entry => entry.Type == LedgerEntryType.Rebuy)
            .ToLookup(entry => entry.TablePlayerId, _ => 1);

        List<StatementRow> statement =
        [
            .. rows
                .OrderByDescending(row => row.ClosedAtUtc)
                .Select(row => new StatementRow
                {
                    TableId = row.Id,
                    TableName = row.Name,
                    ClosedAtUtc = row.ClosedAtUtc,
                    Position = row.Position,
                    Points = row.Points,
                    Balance = row.Balance,
                    PaidIn = paid[row.PlayerId].Sum(),
                    Rebuys = rebuys[row.PlayerId].Count()
                })
        ];

        return new StatementResponse
        {
            Rows = statement,
            TotalBalance = statement.Sum(row => row.Balance),
            TotalPaidIn = statement.Sum(row => row.PaidIn),
            TotalPoints = statement.Sum(row => row.Points),
            Wins = statement.Count(row => row.Position == 1)
        };
    }
}
