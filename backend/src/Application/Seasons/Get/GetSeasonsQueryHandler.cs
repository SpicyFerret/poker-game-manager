using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Seasons.Get;

internal sealed class GetSeasonsQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetSeasonsQuery, IReadOnlyList<SeasonResponse>>
{
    public async Task<Result<IReadOnlyList<SeasonResponse>>> Handle(
        GetSeasonsQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SeasonResponse>>(caller.Error);
        }

        List<SeasonResponse> seasons = await context.Seasons
            .Where(s => s.ChampionshipId == query.ChampionshipId)
            .OrderByDescending(s => s.StartsOn)
            .Select(s => new SeasonResponse
            {
                Id = s.Id,
                Name = s.Name,
                StartsOn = s.StartsOn,
                EndsOn = s.EndsOn
            })
            .ToListAsync(cancellationToken);

        return seasons;
    }
}
