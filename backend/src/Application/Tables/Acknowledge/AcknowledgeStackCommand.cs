using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Acknowledge;

/// <summary>
/// "I have these chips in front of me."
///
/// Only the player themselves: the whole value of the notice is that a second
/// pair of eyes counted the stack, and a manager confirming on their behalf
/// would be the same pair that counted it out.
/// </summary>
public sealed record AcknowledgeStackCommand(Guid ChampionshipId, Guid TableId, Guid LedgerEntryId)
    : ICommand;

internal sealed class AcknowledgeStackCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AcknowledgeStackCommand>
{
    public async Task<Result> Handle(
        AcknowledgeStackCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        LedgerEntry? entry = await context.LedgerEntries
            .SingleOrDefaultAsync(
                e => e.Id == command.LedgerEntryId && e.TableId == command.TableId,
                cancellationToken);

        if (entry is null)
        {
            return Result.Failure(TableErrors.StackNotFound);
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == entry.TablePlayerId,
            cancellationToken);

        if (player is null || player.UserId != userContext.UserId)
        {
            return Result.Failure(TableErrors.CannotAcknowledgeSomeoneElsesStack);
        }

        // Confirming twice is what a double tap looks like, and the first answer
        // is the true one. Keeping it beats moving the timestamp.
        if (entry.AcknowledgedAtUtc is null)
        {
            entry.AcknowledgedAtUtc = dateTimeProvider.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
