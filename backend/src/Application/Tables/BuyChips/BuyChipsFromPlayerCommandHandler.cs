using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.BuyChips;

internal sealed class BuyChipsFromPlayerCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BuyChipsFromPlayerCommand>
{
    public async Task<Result> Handle(BuyChipsFromPlayerCommand command, CancellationToken cancellationToken)
    {
        // Player, not TableManager: buying for yourself needs nobody's
        // permission but your own. Recording a purchase where someone else is
        // the buyer is checked explicitly below, once the buyer is resolved.
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (command.Amount <= 0)
        {
            return Result.Failure(TableErrors.AmountMustBePositive);
        }

        if (command.BuyerPlayerId == command.SellerPlayerId)
        {
            return Result.Failure(TableErrors.CounterpartyIsTheSamePlayer);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == command.TableId && t.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure(TableErrors.NotFound(command.TableId));
        }

        if (table.Status != TableStatus.Running)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Running));
        }

        List<TablePlayer> players = await context.TablePlayers
            .Where(p => p.TableId == table.Id &&
                        (p.Id == command.BuyerPlayerId || p.Id == command.SellerPlayerId))
            .ToListAsync(cancellationToken);

        TablePlayer? buyer = players.SingleOrDefault(p => p.Id == command.BuyerPlayerId);
        TablePlayer? seller = players.SingleOrDefault(p => p.Id == command.SellerPlayerId);

        if (buyer is null || seller is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        if (buyer.UserId != userContext.UserId && caller.Value < ChampionshipRole.TableManager)
        {
            return Result.Failure(TableErrors.CannotBuyChipsForSomeoneElse);
        }

        if (buyer.Status != TablePlayerStatus.Playing)
        {
            return Result.Failure(TableErrors.PlayerNotPlaying);
        }

        if (seller.Status != TablePlayerStatus.Playing)
        {
            return Result.Failure(TableErrors.CounterpartyNotPlaying);
        }

        DateTime now = dateTimeProvider.UtcNow;

        // Two entries, no chip rows on either. The chips changed hands across the
        // table; none came out of the case, so the per-denomination reconciliation
        // at the end is untouched by this.
        context.LedgerEntries.AddRange(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                TablePlayerId = buyer.Id,
                Type = LedgerEntryType.ChipPurchaseFromPlayer,
                MoneyAmount = command.Amount,
                CounterpartyPlayerId = seller.Id,
                CreatedBy = userContext.UserId,
                CreatedAtUtc = now
            },
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                TablePlayerId = seller.Id,
                Type = LedgerEntryType.ChipSaleToPlayer,
                MoneyAmount = command.Amount,
                CounterpartyPlayerId = buyer.Id,
                CreatedBy = userContext.UserId,
                CreatedAtUtc = now
            });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
