using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Authorization;

internal sealed class ChampionshipContext(IApplicationDbContext context, IUserContext userContext)
    : IChampionshipContext
{
    public async Task<ChampionshipRole?> GetRoleAsync(
        Guid championshipId,
        CancellationToken cancellationToken)
    {
        ChampionshipRole[] roles = await context.ChampionshipMembers
            .Where(m => m.ChampionshipId == championshipId && m.UserId == userContext.UserId)
            .Select(m => m.Role)
            .ToArrayAsync(cancellationToken);

        return roles.Length == 0 ? null : roles[0];
    }

    public async Task<Result<ChampionshipRole>> RequireRoleAsync(
        Guid championshipId,
        ChampionshipRole minimum,
        CancellationToken cancellationToken)
    {
        ChampionshipRole? role = await GetRoleAsync(championshipId, cancellationToken);

        if (role is null)
        {
            // Same answer as "no such championship" on purpose: a non-member
            // learns nothing about which ids exist.
            return Result.Failure<ChampionshipRole>(ChampionshipErrors.NotAMember);
        }

        return role < minimum
            ? Result.Failure<ChampionshipRole>(ChampionshipErrors.InsufficientRole(minimum))
            : role.Value;
    }
}
