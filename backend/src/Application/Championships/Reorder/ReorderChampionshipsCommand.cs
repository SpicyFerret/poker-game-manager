using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Reorder;

/// <summary>
/// The caller's own arrangement of the championships they belong to, top to
/// bottom. Personal, not a championship setting — the same championship can
/// sit anywhere in someone else's list.
/// </summary>
public sealed record ReorderChampionshipsCommand(IReadOnlyList<Guid> ChampionshipIds) : ICommand;

internal sealed class ReorderChampionshipsCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext)
    : ICommandHandler<ReorderChampionshipsCommand>
{
    public async Task<Result> Handle(
        ReorderChampionshipsCommand command,
        CancellationToken cancellationToken)
    {
        List<ChampionshipMember> memberships = await context.ChampionshipMembers
            .Where(m => m.UserId == userContext.UserId)
            .ToListAsync(cancellationToken);

        // The list has to be exactly the caller's own memberships — one missing
        // or unknown id would either leave a championship stuck in place or try
        // to move something that is not theirs.
        var given = new HashSet<Guid>(command.ChampionshipIds);
        var actual = new HashSet<Guid>(memberships.Select(m => m.ChampionshipId));

        if (given.Count != command.ChampionshipIds.Count || !given.SetEquals(actual))
        {
            return Result.Failure(ChampionshipErrors.ReorderMustIncludeEveryChampionship);
        }

        var byChampionship = memberships.ToDictionary(m => m.ChampionshipId);

        for (int index = 0; index < command.ChampionshipIds.Count; index++)
        {
            byChampionship[command.ChampionshipIds[index]].DisplayOrder = index;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
