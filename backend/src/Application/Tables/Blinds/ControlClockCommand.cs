using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Blinds;

public enum ClockAction
{
    /// <summary>Starts the clock, or resumes it after a break.</summary>
    Start = 0,
    Pause = 1,
    NextLevel = 2,
    PreviousLevel = 3
}

public sealed record ControlClockCommand(Guid ChampionshipId, Guid TableId, ClockAction Action) : ICommand;

internal sealed class ControlClockCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ControlClockCommand>
{
    public async Task<Result> Handle(ControlClockCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        PokerTable? table = await context.Tables.SingleOrDefaultAsync(
            t => t.Id == command.TableId && t.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (table is null)
        {
            return Result.Failure(TableErrors.NotFound(command.TableId));
        }

        int levelCount = await context.BlindLevels.CountAsync(l => l.TableId == table.Id, cancellationToken);

        if (levelCount == 0)
        {
            return Result.Failure(TableErrors.NoBlindLevels);
        }

        DateTime now = dateTimeProvider.UtcNow;

        TableClock? clock = await context.TableClocks
            .SingleOrDefaultAsync(c => c.TableId == table.Id, cancellationToken);

        if (clock is null)
        {
            // Created on first use rather than when the table opens, so a table
            // that never starts its clock carries no clock at all.
            clock = new TableClock
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                CurrentLevel = 1,
                LevelStartedAtUtc = now,
                PausedAtUtc = command.Action == ClockAction.Start ? null : now
            };

            context.TableClocks.Add(clock);
        }

        switch (command.Action)
        {
            case ClockAction.Start when clock.PausedAtUtc is not null:
                // Fold the break into the accumulated pause, so the level keeps
                // the time it had already been played for.
                clock.AccumulatedPauseSeconds += (int)Math.Max((now - clock.PausedAtUtc.Value).TotalSeconds, 0);
                clock.PausedAtUtc = null;
                break;

            case ClockAction.Pause when clock.PausedAtUtc is null:
                clock.PausedAtUtc = now;
                break;

            case ClockAction.NextLevel:
                clock.CurrentLevel = Math.Min(clock.CurrentLevel + 1, levelCount);
                ResetLevel(clock, now);
                break;

            case ClockAction.PreviousLevel:
                clock.CurrentLevel = Math.Max(clock.CurrentLevel - 1, 1);
                ResetLevel(clock, now);
                break;

            default:
                // Starting an already-running clock, or pausing a paused one, is
                // what a double tap looks like. Doing nothing beats an error.
                break;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// A level change restarts the timer, and clears the pause that belonged to
    /// the level being left — keeping it would shorten the new one.
    /// </summary>
    private static void ResetLevel(TableClock clock, DateTime now)
    {
        clock.LevelStartedAtUtc = now;
        clock.AccumulatedPauseSeconds = 0;

        if (clock.PausedAtUtc is not null)
        {
            clock.PausedAtUtc = now;
        }
    }
}
