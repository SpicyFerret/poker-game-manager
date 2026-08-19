using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Members.ChangeRole;

internal sealed class ChangeMemberRoleCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<ChangeMemberRoleCommand>
{
    public async Task<Result> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        // Owner is not a role you hand out — it moves with the transfer action,
        // and only ever to one person at a time.
        if (command.Role == ChampionshipRole.Owner)
        {
            return Result.Failure(ChampionshipErrors.OwnerRoleIsTransferredNotAssigned);
        }

        ChampionshipMember? member = await context.ChampionshipMembers.SingleOrDefaultAsync(
            m => m.ChampionshipId == command.ChampionshipId && m.UserId == command.UserId,
            cancellationToken);

        if (member is null)
        {
            return Result.Failure(ChampionshipErrors.MemberNotFound);
        }

        // The whole rule, both directions: you may only touch someone strictly
        // below you, and only move them to a role still strictly below you.
        //
        // Without the second half an Admin could promote a Player to Admin and
        // lose the ability to undo it. The first half also covers the caller
        // themselves — your own role is never strictly below your own — so an
        // Owner cannot demote themselves and leave nobody able to transfer.
        if (member.Role >= caller.Value || command.Role >= caller.Value)
        {
            return Result.Failure(ChampionshipErrors.CannotActOnEqualOrHigherRole);
        }

        member.Role = command.Role;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
