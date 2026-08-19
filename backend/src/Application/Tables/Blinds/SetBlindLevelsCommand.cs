using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Blinds;

public sealed record BlindLevelInput(int SmallBlind, int BigBlind, int Ante, int DurationSeconds);

/// <summary>
/// Sets a table's blind ladder. Sending an empty list removes it, which turns the
/// clock off — a table with no levels simply has no clock, and that is how most
/// casual nights run.
/// </summary>
public sealed record SetBlindLevelsCommand(
    Guid ChampionshipId,
    Guid TableId,
    IReadOnlyList<BlindLevelInput> Levels)
    : ICommand;

internal sealed class SetBlindLevelsCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<SetBlindLevelsCommand>
{
    public async Task<Result> Handle(SetBlindLevelsCommand command, CancellationToken cancellationToken)
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

        if (table.Status is not (TableStatus.Open or TableStatus.Running))
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Running));
        }

        if (command.Levels.Any(l => l.SmallBlind <= 0 || l.BigBlind <= 0 || l.Ante < 0 || l.DurationSeconds < 0))
        {
            return Result.Failure(TableErrors.InvalidBlindLevel);
        }

        List<BlindLevel> existing = await context.BlindLevels
            .Where(l => l.TableId == table.Id)
            .ToListAsync(cancellationToken);

        context.BlindLevels.RemoveRange(existing);

        for (int index = 0; index < command.Levels.Count; index++)
        {
            BlindLevelInput level = command.Levels[index];

            context.BlindLevels.Add(new BlindLevel
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                Order = index + 1,
                SmallBlind = level.SmallBlind,
                BigBlind = level.BigBlind,
                Ante = level.Ante,
                DurationSeconds = level.DurationSeconds
            });
        }

        TableClock? clock = await context.TableClocks
            .SingleOrDefaultAsync(c => c.TableId == table.Id, cancellationToken);

        // No levels, no clock. Dropping the ladder while a clock exists would
        // leave it counting against nothing.
        if (command.Levels.Count == 0 && clock is not null)
        {
            context.TableClocks.Remove(clock);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
