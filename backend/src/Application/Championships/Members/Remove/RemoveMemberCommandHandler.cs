using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Members.Remove;

internal sealed class RemoveMemberCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<RemoveMemberCommand>
{
    public async Task<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        ChampionshipMember? member = await context.ChampionshipMembers.SingleOrDefaultAsync(
            m => m.ChampionshipId == command.ChampionshipId && m.UserId == command.UserId,
            cancellationToken);

        if (member is null)
        {
            return Result.Failure(ChampionshipErrors.MemberNotFound);
        }

        if (member.Role == ChampionshipRole.Owner)
        {
            return Result.Failure(ChampionshipErrors.CannotRemoveOwner);
        }

        // Same rule as changing a role: strictly below you, which also stops an
        // Admin from removing themselves or a peer.
        if (member.Role >= caller.Value)
        {
            return Result.Failure(ChampionshipErrors.CannotActOnEqualOrHigherRole);
        }

        context.ChampionshipMembers.Remove(member);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
