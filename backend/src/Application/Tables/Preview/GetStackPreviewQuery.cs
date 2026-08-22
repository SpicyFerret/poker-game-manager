using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Domain.Tables.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Preview;

/// <summary>
/// What chips a buy-in or rebuy would hand over, without handing them over.
///
/// Exists because the person tapping the button is also the person counting the
/// chips out of the case, and they need the list in front of them — before
/// committing, not after.
/// </summary>
public sealed record GetStackPreviewQuery(Guid ChampionshipId, Guid TableId, bool IsRebuy)
    : IQuery<StackPreviewResponse>;

internal sealed class GetStackPreviewQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetStackPreviewQuery, StackPreviewResponse>
{
    public async Task<Result<StackPreviewResponse>> Handle(
        GetStackPreviewQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<StackPreviewResponse>(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure<StackPreviewResponse>(TableErrors.NotFound(query.TableId));
        }

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        List<LedgerEntry> entries = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        // Same stock and same calculator the real issue uses, so the preview
        // cannot promise a mix the actual deal would not produce.
        int reserve = query.IsRebuy ? 0 : table.SmallChipReserve;

        IReadOnlyList<DenominationStock> stock = ChipStock.Available(
            denominations,
            ChipStock.IssuedByDenomination(entries),
            reserve);

        long targetUnits = query.IsRebuy ? table.RebuyUnits : table.BuyInUnits;

        // A buy-in preview on a table that has not started is previewing the
        // opening deal — every waiting player gets a stack at once, not just the
        // one person looking at the screen. Previewing a single stack against the
        // whole case was the bug: it showed a mix built from small chips there
        // were only enough of for the first player or two.
        int stackCount = !query.IsRebuy && table.Status == TableStatus.Open
            ? await context.TablePlayers.CountAsync(
                p => p.TableId == table.Id && p.Status == TablePlayerStatus.Standby,
                cancellationToken)
            : 1;

        stackCount = Math.Max(stackCount, 1);

        // Runs the same deal the start would, so the preview cannot promise a
        // mix the real thing would not produce — including falling back to
        // unequal stacks when the case cannot be split evenly.
        OpeningDeal deal = ChipDistributionCalculator.DealOpeningStacks(
            targetUnits,
            stock,
            stackCount);

        // The first player's stack. It is every player's when the deal came out
        // equal, and the response says which of the two this is. When the deal
        // was refused it is how far the case got before falling short, which is
        // the more useful half of "it does not fit".
        ChipDistribution distribution = deal.Stacks.Count > 0 ? deal.Stacks[0] : deal.Attempted;

        var byId = denominations.ToDictionary(d => d.Id);

        return new StackPreviewResponse
        {
            Money = query.IsRebuy ? table.Rebuy : table.BuyIn,
            Units = targetUnits,
            ShortfallUnits = deal.ShortfallUnits,
            StackCount = stackCount,
            StacksAreEqual = deal.IsEqual || stackCount == 1,
            Chips =
            [
                .. distribution.Chips
                    .Select(chip => new
                    {
                        Chip = chip,
                        Denomination = byId[chip.DenominationId]
                    })
                    // Biggest first: that is the order someone counts a stack out
                    // of a case, and the order they will read it back.
                    .OrderByDescending(pair => pair.Denomination.EffectiveValue)
                    .Select(pair => new StackPreviewChip
                    {
                        DenominationId = pair.Chip.DenominationId,
                        FaceValue = pair.Denomination.FaceValue,
                        EffectiveValue = pair.Denomination.EffectiveValue,
                        Colour = pair.Denomination.Colour,
                        Quantity = pair.Chip.Quantity
                    })
            ]
        };
    }
}
