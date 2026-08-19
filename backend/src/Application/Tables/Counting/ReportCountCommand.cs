using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Counting;

public sealed record ChipCountEntry(Guid DenominationId, int Quantity);

/// <summary>
/// One player's final stack, as they counted it. Reported whole rather than chip
/// by chip: a partial report would read as "counted, and holding nothing of the
/// rest", which is exactly the kind of silent wrong answer the reconciliation is
/// there to catch.
/// </summary>
public sealed record ReportCountCommand(
    Guid ChampionshipId,
    Guid TableId,
    Guid TablePlayerId,
    IReadOnlyList<ChipCountEntry> Counts)
    : ICommand;

internal sealed class ReportCountCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReportCountCommand>
{
    public async Task<Result> Handle(ReportCountCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Player,
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

        if (table.Status != TableStatus.Counting)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Counting));
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == command.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        // Everyone counts their own stack; a manager can enter it for whoever is
        // already out the door.
        bool isOwnStack = player.UserId == userContext.UserId;

        if (!isOwnStack && caller.Value < ChampionshipRole.TableManager)
        {
            return Result.Failure(TableErrors.CannotCountForSomeoneElse);
        }

        if (command.Counts.Any(c => c.Quantity < 0))
        {
            return Result.Failure(TableErrors.QuantityCannotBeNegative);
        }

        HashSet<Guid> validDenominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .Select(d => d.Id)
            .ToHashSetAsync(cancellationToken);

        if (command.Counts.Any(c => !validDenominations.Contains(c.DenominationId)))
        {
            return Result.Failure(TableErrors.DenominationNotInThisCase);
        }

        // Replaced wholesale, so a corrected count overwrites the wrong one rather
        // than adding to it.
        List<FinalCount> existing = await context.FinalCounts
            .Where(c => c.TablePlayerId == player.Id)
            .ToListAsync(cancellationToken);

        context.FinalCounts.RemoveRange(existing);

        DateTime now = dateTimeProvider.UtcNow;

        foreach (ChipCountEntry entry in command.Counts.Where(c => c.Quantity > 0))
        {
            context.FinalCounts.Add(new FinalCount
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                TablePlayerId = player.Id,
                ChipDenominationId = entry.DenominationId,
                Quantity = entry.Quantity,
                ReportedAtUtc = now
            });
        }

        // Someone holding nothing still has to say so, or the table would wait
        // forever for them. A zero-chip marker records "counted, holding none".
        if (command.Counts.All(c => c.Quantity == 0))
        {
            context.FinalCounts.Add(new FinalCount
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                TablePlayerId = player.Id,
                ChipDenominationId = validDenominations.First(),
                Quantity = 0,
                ReportedAtUtc = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
