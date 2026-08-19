using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Members.Add;

internal sealed class AddMemberCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddMemberCommand>
{
    public async Task<Result> Handle(AddMemberCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (command.Role >= caller.Value)
        {
            return Result.Failure(ChampionshipErrors.CannotActOnEqualOrHigherRole);
        }

        string email = command.Email.Trim();

        Guid userId = await context.Users
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (userId == Guid.Empty)
        {
            return Result.Failure(UserErrors.NotFoundByEmail);
        }

        bool alreadyAMember = await context.ChampionshipMembers.AnyAsync(
            m => m.ChampionshipId == command.ChampionshipId && m.UserId == userId,
            cancellationToken);

        if (alreadyAMember)
        {
            return Result.Failure(ChampionshipErrors.AlreadyAMember);
        }

        context.ChampionshipMembers.Add(new ChampionshipMember
        {
            Id = Guid.NewGuid(),
            ChampionshipId = command.ChampionshipId,
            UserId = userId,
            Role = command.Role,
            JoinedAtUtc = dateTimeProvider.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
