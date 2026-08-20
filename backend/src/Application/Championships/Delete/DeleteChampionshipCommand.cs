using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Delete;

/// <summary>
/// Removes a championship and everything in it: members, invites, chip cases,
/// every table ever played and their results.
///
/// Owner only, and the name has to be typed. This is the most destructive thing
/// in the system — the championship is the ranking window, so this takes a whole
/// year of rankings with it — and the confirmation is deliberately something you
/// cannot do by accident.
/// </summary>
public sealed record DeleteChampionshipCommand(Guid ChampionshipId, string ConfirmName) : ICommand;

internal sealed class DeleteChampionshipCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<DeleteChampionshipCommand>
{
    public async Task<Result> Handle(
        DeleteChampionshipCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Owner,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        Championship? championship = await context.Championships
            .SingleOrDefaultAsync(c => c.Id == command.ChampionshipId, cancellationToken);

        if (championship is null)
        {
            return Result.Failure(ChampionshipErrors.NotFound(command.ChampionshipId));
        }

        if (!string.Equals(command.ConfirmName.Trim(), championship.Name, StringComparison.Ordinal))
        {
            return Result.Failure(ChampionshipErrors.ConfirmationDoesNotMatch);
        }

        List<Guid> tableIds = await context.Tables
            .Where(t => t.ChampionshipId == championship.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        // Deleted in dependency order by hand. The database would cascade most of
        // it, but the settlement transfers and the ledger's counterparty both
        // point at table_players, and leaving the order to the cascade is exactly
        // what deadlocked before.
        context.TableResults.RemoveRange(
            await context.TableResults.Where(r => tableIds.Contains(r.TableId)).ToListAsync(cancellationToken));

        context.Settlements.RemoveRange(
            await context.Settlements
                .Include(s => s.Transfers)
                .Where(s => tableIds.Contains(s.TableId))
                .ToListAsync(cancellationToken));

        context.FinalCounts.RemoveRange(
            await context.FinalCounts.Where(c => tableIds.Contains(c.TableId)).ToListAsync(cancellationToken));

        context.LedgerEntries.RemoveRange(
            await context.LedgerEntries
                .Include(e => e.Chips)
                .Where(e => tableIds.Contains(e.TableId))
                .ToListAsync(cancellationToken));

        context.TablePlayers.RemoveRange(
            await context.TablePlayers.Where(p => tableIds.Contains(p.TableId)).ToListAsync(cancellationToken));

        context.BlindLevels.RemoveRange(
            await context.BlindLevels.Where(l => tableIds.Contains(l.TableId)).ToListAsync(cancellationToken));

        context.TableClocks.RemoveRange(
            await context.TableClocks.Where(c => tableIds.Contains(c.TableId)).ToListAsync(cancellationToken));

        context.Tables.RemoveRange(
            await context.Tables.Where(t => tableIds.Contains(t.Id)).ToListAsync(cancellationToken));

        // The rest hangs off the championship and cascades cleanly.
        context.Championships.Remove(championship);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
