using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Domain.Tables.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Counting;

public sealed record ReconciliationLine
{
    public Guid DenominationId { get; init; }
    public int FaceValue { get; init; }
    public int EffectiveValue { get; init; }
    public int Issued { get; init; }
    public int Counted { get; init; }
    public int Difference { get; init; }
}

public sealed record AwaitingPlayer(Guid TablePlayerId, string DisplayName);

public sealed record ReconciliationResponse
{
    public IReadOnlyList<ReconciliationLine> Lines { get; init; } = [];
    public IReadOnlyList<AwaitingPlayer> AwaitingCountFrom { get; init; } = [];
    public bool EveryoneHasCounted { get; init; }
    public bool ChipsBalance { get; init; }
    public bool CanSettle { get; init; }
}

public sealed record GetReconciliationQuery(Guid ChampionshipId, Guid TableId)
    : IQuery<ReconciliationResponse>;

internal sealed class GetReconciliationQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetReconciliationQuery, ReconciliationResponse>
{
    public async Task<Result<ReconciliationResponse>> Handle(
        GetReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<ReconciliationResponse>(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure<ReconciliationResponse>(TableErrors.NotFound(query.TableId));
        }

        TableReconciliation reconciliation = await BuildAsync(context, table, cancellationToken);

        List<TablePlayer> players = await context.TablePlayers
            .Include(p => p.User)
            .Where(p => p.TableId == table.Id)
            .ToListAsync(cancellationToken);

        return new ReconciliationResponse
        {
            Lines =
            [
                .. reconciliation.Denominations.Select(d => new ReconciliationLine
                {
                    DenominationId = d.DenominationId,
                    FaceValue = d.FaceValue,
                    EffectiveValue = d.EffectiveValue,
                    Issued = d.Issued,
                    Counted = d.Counted,
                    Difference = d.Difference
                })
            ],
            AwaitingCountFrom =
            [
                .. reconciliation.AwaitingCountFrom
                    .Select(id => players.Single(p => p.Id == id))
                    .OrderBy(p => p.SeatOrder)
                    .Select(p => new AwaitingPlayer(p.Id, p.User.DisplayName))
            ],
            EveryoneHasCounted = reconciliation.EveryoneHasCounted,
            ChipsBalance = reconciliation.ChipsBalance,
            CanSettle = reconciliation.CanReconcile
        };
    }

    /// <summary>
    /// Shared with settling, so the panel a manager reads and the gate the API
    /// enforces can never disagree about whether the table balances.
    /// </summary>
    internal static async Task<TableReconciliation> BuildAsync(
        IApplicationDbContext context,
        PokerTable table,
        CancellationToken cancellationToken)
    {
        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        List<LedgerEntry> entries = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        List<FinalCount> counts = await context.FinalCounts
            .Where(c => c.TableId == table.Id)
            .ToListAsync(cancellationToken);

        // Everyone who was ever dealt in owes a count, including anyone who left
        // early — their chips left the case just the same. Stated as who has
        // played rather than as who has not sat down, so someone still waiting
        // on a manager's answer is never counted as owing anything.
        List<Guid> expected = await context.TablePlayers
            .Where(p => p.TableId == table.Id &&
                        (p.Status == TablePlayerStatus.Playing || p.Status == TablePlayerStatus.Left))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return TableReconciliationService.Build(
            [.. denominations.Select(d => (d.Id, d.FaceValue, d.EffectiveValue))],
            ChipStock.IssuedByDenomination(entries),
            counts.GroupBy(c => c.ChipDenominationId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity)),
            expected,
            [.. counts.Select(c => c.TablePlayerId).Distinct()]);
    }
}
