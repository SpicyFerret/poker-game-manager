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

        // Once counting starts the roster is what the reconciliation is built
        // from, and removing a row would change what the night has to add up to.
        if (table.Status != TableStatus.Open && table.Status != TableStatus.Running)
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

        // The real question is not what status they are in but whether anything
        // has moved on their behalf. A ledger entry means they paid in and chips
        // left the case; deleting them then would leave those chips belonging to
        // nobody, and the table could never be reconciled. Someone who is done
        // playing leaves the table, they do not vanish from its books.
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
