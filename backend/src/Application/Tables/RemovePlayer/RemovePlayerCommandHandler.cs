using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.RemovePlayer;

internal sealed class RemovePlayerCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<RemovePlayerCommand>
{
    public async Task<Result> Handle(RemovePlayerCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.TableManager,
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

        // Only before the first card is dealt. Once a table is running, taking
        // someone off it is a decision about a night in progress rather than a
        // correction to who turned up, and the way out of a running table is to
        // cash out — which leaves a record — not to disappear from it.
        if (table.Status != TableStatus.Open)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Open));
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == command.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        // Belt and braces. The status gate above already keeps this out of reach
        // — chips only leave the case once a table is Running — but this is the
        // condition that actually matters, stated against the books rather than
        // by proxy: a ledger entry means chips left the case for this player, and
        // deleting them would leave those chips belonging to nobody.
        bool hasLedger = await context.LedgerEntries.AnyAsync(
            e => e.TablePlayerId == player.Id,
            cancellationToken);

        if (hasLedger)
        {
            return Result.Failure(TableErrors.CannotRemoveAPlayerWhoHasChips);
        }

        context.TablePlayers.Remove(player);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
