using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Get;

internal sealed class GetTableQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext)
    : IQueryHandler<GetTableQuery, TableDetailResponse>
{
    public async Task<Result<TableDetailResponse>> Handle(
        GetTableQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<TableDetailResponse>(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure<TableDetailResponse>(TableErrors.NotFound(query.TableId));
        }

        bool canManage = caller.Value >= ChampionshipRole.TableManager;

        List<TablePlayer> players = await context.TablePlayers
            .Include(p => p.User)
            .Where(p => p.TableId == table.Id)
            .OrderBy(p => p.SeatOrder)
            .ToListAsync(cancellationToken);

        List<LedgerEntry> entries = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .OrderBy(d => d.EffectiveValue)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> issued = ChipStock.IssuedByDenomination(entries);

        ILookup<Guid, LedgerEntry> byPlayer = entries.ToLookup(e => e.TablePlayerId);

        List<TablePlayerResponse> playerResponses =
        [
            .. players.Select(player => new TablePlayerResponse
            {
                TablePlayerId = player.Id,
                UserId = player.UserId,
                DisplayName = player.User.DisplayName,
                Status = player.Status,
                SeatOrder = player.SeatOrder,
                PaidIn = PaidInFor(byPlayer[player.Id]),
                RebuyCount = byPlayer[player.Id].Count(e => e.Type == LedgerEntryType.Rebuy),
                HasPaymentHandle = player.User.PaymentHandle != null
            })
        ];

        Guid? myPlayerId = players.SingleOrDefault(p => p.UserId == userContext.UserId)?.Id;

        return new TableDetailResponse
        {
            Id = table.Id,
            ChampionshipId = table.ChampionshipId,
            Name = table.Name,
            Status = table.Status,
            BuyIn = table.BuyIn,
            Rebuy = table.Rebuy,
            MoneyPerUnit = table.MoneyPerUnit,
            BuyInUnits = table.BuyInUnits,
            JoinPolicy = table.JoinPolicy,
            AllowLateEntry = table.AllowLateEntry,
            JoinCode = canManage ? table.JoinCode : null,
            SmallChipReserve = table.SmallChipReserve,
            StartedAtUtc = table.StartedAtUtc,
            Players = playerResponses,
            Stock =
            [
                .. denominations.Select(d => new ChipStockResponse
                {
                    DenominationId = d.Id,
                    FaceValue = d.FaceValue,
                    EffectiveValue = d.EffectiveValue,
                    Colour = d.Colour,
                    Issued = issued.GetValueOrDefault(d.Id),
                    Remaining = Math.Max(d.Quantity - issued.GetValueOrDefault(d.Id), 0)
                })
            ],
            TotalPaidIn = playerResponses.Sum(p => p.PaidIn),
            CanManage = canManage,
            MyPlayerId = myPlayerId,
            // Only while the table is live. A notice about a night that has
            // already been counted and settled is noise, not a check.
            PendingStacks = myPlayerId is null || table.Status is not (TableStatus.Open or TableStatus.Running)
                ? []
                : PendingFor(byPlayer[myPlayerId.Value], denominations)
        };
    }

    /// <summary>
    /// The caller's own unacknowledged stacks, oldest first. Only entries that
    /// actually took chips out of the case: there is nothing to count for a trade
    /// between two players, since those chips were already on the table.
    /// </summary>
    private static List<PendingStackResponse> PendingFor(
        IEnumerable<LedgerEntry> entries,
        List<ChipDenomination> denominations) =>
    [
        .. entries
            .Where(entry =>
                entry.AcknowledgedAtUtc is null &&
                entry.Chips.Count > 0 &&
                entry.Type is LedgerEntryType.BuyIn or LedgerEntryType.Rebuy)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new PendingStackResponse
            {
                LedgerEntryId = entry.Id,
                IsRebuy = entry.Type == LedgerEntryType.Rebuy,
                Money = entry.MoneyAmount,
                Chips =
                [
                    // Biggest first, which is how anyone stacks chips to count them.
                    .. entry.Chips
                        .Join(
                            denominations,
                            chip => chip.ChipDenominationId,
                            denomination => denomination.Id,
                            (chip, denomination) => new StackPreviewChip
                            {
                                DenominationId = denomination.Id,
                                FaceValue = denomination.FaceValue,
                                EffectiveValue = denomination.EffectiveValue,
                                Colour = denomination.Colour,
                                Quantity = chip.Quantity
                            })
                        .OrderByDescending(chip => chip.EffectiveValue)
                ]
            })
    ];

    /// <summary>
    /// PaidIn = buy-ins + rebuys + chips bought off others, less anything credited
    /// for chips sold. Selling chips to someone whose rebuy the case could not
    /// cover reduces what you are down, which is what stops the seller being out
    /// of pocket for bailing the table out.
    /// </summary>
    private static decimal PaidInFor(IEnumerable<LedgerEntry> entries) =>
        entries.Sum(entry => entry.Type switch
        {
            LedgerEntryType.BuyIn or
            LedgerEntryType.Rebuy or
            LedgerEntryType.ChipPurchaseFromPlayer => entry.MoneyAmount,
            LedgerEntryType.ChipSaleToPlayer => -entry.MoneyAmount,
            LedgerEntryType.Adjustment => entry.MoneyAmount,
            _ => 0m
        });
}
