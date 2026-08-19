using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Seasons.Create;

internal sealed class CreateSeasonCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<CreateSeasonCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSeasonCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        if (command.EndsOn is not null && command.EndsOn < command.StartsOn)
        {
            return Result.Failure<Guid>(SeasonErrors.EndsBeforeItStarts);
        }

        List<Season> siblings = await context.Seasons
            .Where(s => s.ChampionshipId == command.ChampionshipId)
            .ToListAsync(cancellationToken);

        // Overlapping seasons would let one table's result count towards two
        // rankings, so the totals would stop adding up. An open-ended season
        // (EndsOn null) runs forever, so it collides with anything that starts
        // after it.
        bool overlaps = siblings.Any(existing =>
            command.StartsOn <= (existing.EndsOn ?? DateOnly.MaxValue) &&
            existing.StartsOn <= (command.EndsOn ?? DateOnly.MaxValue));

        if (overlaps)
        {
            return Result.Failure<Guid>(SeasonErrors.Overlaps);
        }

        var season = new Season
        {
            Id = Guid.NewGuid(),
            ChampionshipId = command.ChampionshipId,
            Name = command.Name.Trim(),
            StartsOn = command.StartsOn,
            EndsOn = command.EndsOn
        };

        context.Seasons.Add(season);

        await context.SaveChangesAsync(cancellationToken);

        return season.Id;
    }
}
