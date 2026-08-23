using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Tables.Counting;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.CashOut;

internal sealed class CashOutCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CashOutCommand>
{
    public async Task<Result> Handle(CashOutCommand command, CancellationToken cancellationToken)
    {
        // Player, not TableManager: going home is your own decision, the same
        // way a rebuy is. Cashing someone *else* out is checked below.
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == command.TableId && t.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure(TableErrors.NotFound(command.TableId));
        }

        // Cashing out is a mid-game act. Once counting has started everybody is
        // reporting anyway, and that is the path to use.
        if (table.Status != TableStatus.Running)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Running));
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == command.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        if (player.UserId != userContext.UserId && caller.Value < ChampionshipRole.TableManager)
        {
            return Result.Failure(TableErrors.CannotCashOutSomeoneElse);
        }

        if (player.Status != TablePlayerStatus.Playing)
        {
            return Result.Failure(TableErrors.PlayerNotPlaying);
        }

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        var byId = denominations.ToDictionary(d => d.Id);

        foreach (ChipCountEntry entry in command.Counts)
        {
            if (entry.Quantity < 0)
            {
                return Result.Failure(TableErrors.QuantityCannotBeNegative);
            }

            if (!byId.ContainsKey(entry.DenominationId))
            {
                return Result.Failure(TableErrors.DenominationNotInThisCase);
            }
        }

        List<LedgerEntry> existing = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        // Handing back more of a chip than the whole table was ever given is not
        // a cash-out, it is a miscount — and letting it through would drive the
        // issued total negative and quietly break the reconciliation for
        // everyone else still playing.
        Dictionary<Guid, int> issued = ChipStock.IssuedByDenomination(existing);

        foreach (ChipCountEntry entry in command.Counts.Where(c => c.Quantity > 0))
        {
            if (entry.Quantity > issued.GetValueOrDefault(entry.DenominationId))
            {
                return Result.Failure(TableErrors.CashOutMoreThanIsInPlay);
            }
        }

        long units = command.Counts.Sum(c => (long)c.Quantity * byId[c.DenominationId].EffectiveValue);
        decimal money = units * table.MoneyPerUnit;

        DateTime now = dateTimeProvider.UtcNow;

        context.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            TablePlayerId = player.Id,
            Type = LedgerEntryType.CashOut,
            MoneyAmount = money,
            CreatedBy = userContext.UserId,
            CreatedAtUtc = now,
            // Nothing to acknowledge: these chips are going the other way, and
            // the person handing them over is the one counting them.
            AcknowledgedAtUtc = now,
            Chips =
            [
                .. command.Counts
                    .Where(c => c.Quantity > 0)
                    .Select(c => new LedgerEntryChip
                    {
                        Id = Guid.NewGuid(),
                        ChipDenominationId = c.DenominationId,
                        Quantity = c.Quantity
                    })
            ]
        });

        // A zero final count, recorded now. They hold nothing from here on, and
        // without this the end-of-night reconciliation would sit waiting for a
        // count from someone who already went home.
        context.FinalCounts.Add(new FinalCount
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            TablePlayerId = player.Id,
            ChipDenominationId = denominations[0].Id,
            Quantity = 0,
            ReportedAtUtc = now
        });

        player.Status = TablePlayerStatus.Left;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
