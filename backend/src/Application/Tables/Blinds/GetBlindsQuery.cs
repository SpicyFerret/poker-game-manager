using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Blinds;

public sealed record GetBlindsQuery(Guid ChampionshipId, Guid TableId) : IQuery<BlindsResponse>;

internal sealed class GetBlindsQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetBlindsQuery, BlindsResponse>
{
    public async Task<Result<BlindsResponse>> Handle(
        GetBlindsQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<BlindsResponse>(caller.Error);
        }

        bool tableExists = await context.Tables.AnyAsync(
            t => t.Id == query.TableId && t.ChampionshipId == query.ChampionshipId,
            cancellationToken);

        if (!tableExists)
        {
            return Result.Failure<BlindsResponse>(TableErrors.NotFound(query.TableId));
        }

        List<BlindLevelResponse> levels = await context.BlindLevels
            .Where(l => l.TableId == query.TableId)
            .OrderBy(l => l.Order)
            .Select(l => new BlindLevelResponse
            {
                Order = l.Order,
                SmallBlind = l.SmallBlind,
                BigBlind = l.BigBlind,
                Ante = l.Ante,
                DurationSeconds = l.DurationSeconds
            })
            .ToListAsync(cancellationToken);

        TableClock? clock = await context.TableClocks
            .SingleOrDefaultAsync(c => c.TableId == query.TableId, cancellationToken);

        DateTime now = dateTimeProvider.UtcNow;

        return new BlindsResponse
        {
            Levels = levels,
            Clock = clock is null
                ? null
                : new TableClockResponse
                {
                    CurrentLevel = clock.CurrentLevel,
                    IsPaused = clock.IsPaused,
                    ElapsedSeconds = clock.ElapsedSeconds(now),
                    ServerTimeUtc = now
                }
        };
    }
}
