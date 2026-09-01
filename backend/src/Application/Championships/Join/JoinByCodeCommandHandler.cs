using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Realtime;
using Application.Championships;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Join;

internal sealed class JoinByCodeCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IChampionshipActivityNotifier notifier)
    : ICommandHandler<JoinByCodeCommand, JoinByCodeResponse>
{
    public async Task<Result<JoinByCodeResponse>> Handle(
        JoinByCodeCommand command,
        CancellationToken cancellationToken)
    {
        string code = InviteCode.Normalize(command.Code);

        if (!InviteCode.IsWellFormed(code))
        {
            return Result.Failure<JoinByCodeResponse>(InviteErrors.NotUsable);
        }

        Invite? invite = await context.Invites.SingleOrDefaultAsync(
            i => i.Code == code,
            cancellationToken);

        if (invite is null || !invite.IsUsable(dateTimeProvider.UtcNow))
        {
            return Result.Failure<JoinByCodeResponse>(InviteErrors.NotUsable);
        }

        string? name = await context.Championships
            .Where(c => c.Id == invite.ChampionshipId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (name is null)
        {
            return Result.Failure<JoinByCodeResponse>(InviteErrors.NotUsable);
        }

        bool alreadyAMember = await context.ChampionshipMembers.AnyAsync(
            m => m.ChampionshipId == invite.ChampionshipId && m.UserId == userContext.UserId,
            cancellationToken);

        // Redeeming a code you already used is treated as success and consumes
        // nothing. Someone tapping the group's link a second time should land in
        // the championship, not read an error — and it stops a double tap from
        // burning a use.
        if (alreadyAMember)
        {
            return new JoinByCodeResponse(invite.ChampionshipId, name);
        }

        context.ChampionshipMembers.Add(new ChampionshipMember
        {
            Id = Guid.NewGuid(),
            ChampionshipId = invite.ChampionshipId,
            UserId = userContext.UserId,
            Role = invite.Role,
            JoinedAtUtc = dateTimeProvider.UtcNow,
            DisplayOrder = await ChampionshipMemberOrdering.NextDisplayOrderAsync(
                context,
                userContext.UserId,
                cancellationToken)
        });

        invite.Uses++;

        await context.SaveChangesAsync(cancellationToken);

        await notifier.NotifyAsync(invite.ChampionshipId, cancellationToken);

        return new JoinByCodeResponse(invite.ChampionshipId, name);
    }
}
