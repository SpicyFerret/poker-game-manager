using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Requests;

internal sealed class DecideJoinRequestCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<DecideJoinRequestCommand>
{
    public async Task<Result> Handle(
        DecideJoinRequestCommand command,
        CancellationToken cancellationToken)
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

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == command.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        // Only a pending request can be answered. Anything else — already
        // seated, already playing, already turned away — is not a question
        // waiting on anyone.
        if (player.Status != TablePlayerStatus.Requested)
        {
            return Result.Failure(TableErrors.NoJoinRequestPending);
        }

        if (command.Approved)
        {
            // Standby, not Playing: approving lets them sit down, and chips
            // still only move when a manager deals them in.
            player.Status = TablePlayerStatus.Standby;
        }
        else
        {
            context.TablePlayers.Remove(player);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
