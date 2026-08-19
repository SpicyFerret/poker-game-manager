using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.TransferOwnership;

internal sealed class TransferOwnershipCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext)
    : ICommandHandler<TransferOwnershipCommand>
{
    public async Task<Result> Handle(
        TransferOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Owner,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        Championship? championship = await context.Championships
            .SingleOrDefaultAsync(c => c.Id == command.ChampionshipId, cancellationToken);

        if (championship is null)
        {
            return Result.Failure(ChampionshipErrors.NotFound(command.ChampionshipId));
        }

        List<ChampionshipMember> members = await context.ChampionshipMembers
            .Where(m => m.ChampionshipId == command.ChampionshipId &&
                        (m.UserId == command.NewOwnerId || m.UserId == userContext.UserId))
            .ToListAsync(cancellationToken);

        ChampionshipMember? newOwner = members.SingleOrDefault(m => m.UserId == command.NewOwnerId);

        if (newOwner is null)
        {
            return Result.Failure(ChampionshipErrors.MemberNotFound);
        }

        // Admin-only: handing the championship to someone who has never run
        // anything in it is almost always a misclick, and promoting them first is
        // one extra deliberate step.
        if (newOwner.Role != ChampionshipRole.Admin)
        {
            return Result.Failure(ChampionshipErrors.NewOwnerMustBeAdmin);
        }

        ChampionshipMember previousOwner = members.Single(m => m.UserId == userContext.UserId);

        newOwner.Role = ChampionshipRole.Owner;

        // Stepping down to Admin rather than leaving: the outgoing owner keeps
        // working, and the championship is never left with two owners or none.
        previousOwner.Role = ChampionshipRole.Admin;
        championship.OwnerId = newOwner.UserId;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
