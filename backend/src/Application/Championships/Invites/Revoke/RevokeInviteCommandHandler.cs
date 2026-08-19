using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Invites.Revoke;

internal sealed class RevokeInviteCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<RevokeInviteCommand>
{
    public async Task<Result> Handle(RevokeInviteCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        Invite? invite = await context.Invites.SingleOrDefaultAsync(
            i => i.Id == command.InviteId && i.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (invite is null)
        {
            return Result.Failure(InviteErrors.NotFound);
        }

        // Revoked rather than deleted: the code stays taken, so a later invite
        // cannot be issued with the same one and quietly resurrect a link someone
        // still has in their chat history.
        invite.IsRevoked = true;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
