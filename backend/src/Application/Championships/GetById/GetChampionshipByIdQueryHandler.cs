using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Championships.Rankings;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.GetById;

internal sealed class GetChampionshipByIdQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetChampionshipByIdQuery, ChampionshipResponse>
{
    public async Task<Result<ChampionshipResponse>> Handle(
        GetChampionshipByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Any member may read the championship, so Player is the bar.
        Result<ChampionshipRole> role = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (role.IsFailure)
        {
            return Result.Failure<ChampionshipResponse>(role.Error);
        }

        ChampionshipResponse? championship = await context.Championships
            .Where(c => c.Id == query.ChampionshipId)
            .Select(c => new ChampionshipResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                OwnerId = c.OwnerId,
                DefaultBuyIn = c.DefaultBuyIn,
                DefaultRebuy = c.DefaultRebuy,
                EnforceDefaults = c.EnforceDefaults,
                MoneyPerUnit = c.MoneyPerUnit,
                PointsByPosition = c.PointsByPosition,
                Role = role.Value
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (championship is null)
        {
            return Result.Failure<ChampionshipResponse>(
                ChampionshipErrors.NotFound(query.ChampionshipId));
        }

        Dictionary<Guid, ChampionshipLeader> leaders = await ChampionshipLeaders.ForAsync(
            context,
            [championship.Id],
            cancellationToken);

        return leaders.TryGetValue(championship.Id, out ChampionshipLeader? leader)
            ? championship with { LeaderDisplayName = leader.DisplayName, LeaderPoints = leader.Points }
            : championship;
    }
}
