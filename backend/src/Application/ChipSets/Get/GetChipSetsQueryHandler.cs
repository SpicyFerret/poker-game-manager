using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ChipSets.Get;

internal sealed class GetChipSetsQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetChipSetsQuery, IReadOnlyList<ChipSetResponse>>
{
    public async Task<Result<IReadOnlyList<ChipSetResponse>>> Handle(
        GetChipSetsQuery query,
        CancellationToken cancellationToken)
    {
        // Any player may look: knowing what the case holds is part of playing.
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChipSetResponse>>(caller.Error);
        }

        List<ChipSetResponse> chipSets = await context.ChipSets
            .Where(s => s.ChampionshipId == query.ChampionshipId)
            .OrderBy(s => s.Name)
            .Select(s => new ChipSetResponse
            {
                Id = s.Id,
                Name = s.Name,
                TotalUnits = s.Denominations.Sum(d => (long)d.EffectiveValue * d.Quantity),
                Denominations = s.Denominations
                    .OrderBy(d => d.EffectiveValue)
                    .Select(d => new ChipDenominationResponse
                    {
                        Id = d.Id,
                        FaceValue = d.FaceValue,
                        EffectiveValue = d.EffectiveValue,
                        Quantity = d.Quantity,
                        Colour = d.Colour
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return chipSets;
    }
}
