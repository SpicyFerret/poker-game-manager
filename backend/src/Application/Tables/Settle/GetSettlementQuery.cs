using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Settle;

public sealed record TransferResponse
{
    public string FromDisplayName { get; init; }
    public string ToDisplayName { get; init; }
    public decimal Amount { get; init; }

    /// <summary>
    /// Where to send it. Only the recipient's, and only on a table the caller
    /// played at — a payment handle has no business being readable from anywhere
    /// else.
    /// </summary>
    public PaymentHandleType? ToPaymentType { get; init; }
    public string? ToPaymentHandle { get; init; }
}

public sealed record ResultResponse
{
    public Guid TablePlayerId { get; init; }
    public string DisplayName { get; init; }
    public int Position { get; init; }
    public int Points { get; init; }
    public decimal Balance { get; init; }
}

public sealed record SettlementResponse
{
    public IReadOnlyList<TransferResponse> Transfers { get; init; } = [];
    public IReadOnlyList<ResultResponse> Results { get; init; } = [];
}

public sealed record GetSettlementQuery(Guid ChampionshipId, Guid TableId) : IQuery<SettlementResponse>;

internal sealed class GetSettlementQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetSettlementQuery, SettlementResponse>
{
    public async Task<Result<SettlementResponse>> Handle(
        GetSettlementQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<SettlementResponse>(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure<SettlementResponse>(TableErrors.NotFound(query.TableId));
        }

        Dictionary<Guid, TablePlayer> players = await context.TablePlayers
            .Include(p => p.User)
            .Where(p => p.TableId == table.Id)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        Settlement? settlement = await context.Settlements
            .Include(s => s.Transfers)
            .SingleOrDefaultAsync(s => s.TableId == table.Id, cancellationToken);

        List<TableResult> results = await context.TableResults
            .Where(r => r.TableId == table.Id)
            .OrderBy(r => r.Position)
            .ToListAsync(cancellationToken);

        return new SettlementResponse
        {
            Transfers =
            [
                .. (settlement?.Transfers ?? [])
                    .OrderByDescending(t => t.Amount)
                    .Select(t => new TransferResponse
                    {
                        FromDisplayName = players[t.FromPlayerId].User.DisplayName,
                        ToDisplayName = players[t.ToPlayerId].User.DisplayName,
                        Amount = t.Amount,
                        ToPaymentType = players[t.ToPlayerId].User.PaymentType,
                        ToPaymentHandle = players[t.ToPlayerId].User.PaymentHandle
                    })
            ],
            Results =
            [
                .. results.Select(r => new ResultResponse
                {
                    TablePlayerId = r.TablePlayerId,
                    DisplayName = players[r.TablePlayerId].User.DisplayName,
                    Position = r.Position,
                    Points = r.Points,
                    Balance = r.Balance
                })
            ]
        };
    }
}
