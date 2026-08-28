using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.StackHistory;

/// <summary>
/// Every buy-in and rebuy a player was actually dealt, chips and all, newest
/// first — the running answer to "what did I get handed tonight", for someone
/// who wants to check it against what's in front of them without waiting for a
/// fresh notice.
/// </summary>
public sealed record GetPlayerStacksQuery(Guid ChampionshipId, Guid TableId, Guid TablePlayerId)
    : IQuery<IReadOnlyList<StackHistoryEntryResponse>>;

internal sealed class GetPlayerStacksQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext)
    : IQueryHandler<GetPlayerStacksQuery, IReadOnlyList<StackHistoryEntryResponse>>
{
    public async Task<Result<IReadOnlyList<StackHistoryEntryResponse>>> Handle(
        GetPlayerStacksQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<StackHistoryEntryResponse>>(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure<IReadOnlyList<StackHistoryEntryResponse>>(TableErrors.NotFound(query.TableId));
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == query.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure<IReadOnlyList<StackHistoryEntryResponse>>(TableErrors.NotAPlayer);
        }

        bool isOwnStack = player.UserId == userContext.UserId;

        if (!isOwnStack && caller.Value < ChampionshipRole.TableManager)
        {
            return Result.Failure<IReadOnlyList<StackHistoryEntryResponse>>(
                TableErrors.CannotViewSomeoneElsesStacks);
        }

        List<LedgerEntry> entries = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TablePlayerId == player.Id &&
                        (e.Type == LedgerEntryType.BuyIn || e.Type == LedgerEntryType.Rebuy))
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        var byId = denominations.ToDictionary(d => d.Id);

        List<StackHistoryEntryResponse> result =
        [
            .. entries.Select(entry => new StackHistoryEntryResponse
            {
                LedgerEntryId = entry.Id,
                IsRebuy = entry.Type == LedgerEntryType.Rebuy,
                Money = entry.MoneyAmount,
                CreatedAtUtc = entry.CreatedAtUtc,
                Chips =
                [
                    .. entry.Chips
                        .Where(chip => byId.ContainsKey(chip.ChipDenominationId))
                        .Select(chip => new StackPreviewChip
                        {
                            DenominationId = chip.ChipDenominationId,
                            FaceValue = byId[chip.ChipDenominationId].FaceValue,
                            EffectiveValue = byId[chip.ChipDenominationId].EffectiveValue,
                            Colour = byId[chip.ChipDenominationId].Colour,
                            Quantity = chip.Quantity
                        })
                        .OrderByDescending(chip => chip.EffectiveValue)
                ]
            })
        ];

        return result;
    }
}

/// <summary>One buy-in or rebuy, with the chips it actually handed over.</summary>
public sealed record StackHistoryEntryResponse
{
    public Guid LedgerEntryId { get; init; }
    public bool IsRebuy { get; init; }
    public decimal Money { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<StackPreviewChip> Chips { get; init; } = [];
}
