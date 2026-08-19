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
            MyPlayerId = players.SingleOrDefault(p => p.UserId == userContext.UserId)?.Id
        };
    }

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
