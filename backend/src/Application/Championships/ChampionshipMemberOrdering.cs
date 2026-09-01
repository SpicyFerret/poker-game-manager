using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Championships;

/// <summary>
/// Where a newly joined championship lands in a user's own list: after
/// everything they already belong to, never in the middle of an order they
/// arranged themselves.
/// </summary>
internal static class ChampionshipMemberOrdering
{
    public static async Task<int> NextDisplayOrderAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        int? highest = await context.ChampionshipMembers
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (highest ?? -1) + 1;
    }
}
