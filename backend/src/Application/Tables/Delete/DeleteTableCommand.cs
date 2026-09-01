using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Realtime;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Delete;

/// <summary>
/// Removes a table and everything that hangs off it — players, ledger, counts,
/// settlement, results.
///
/// <paramref name="ConfirmName"/> must match the table's name. Typing it is the
/// only thing standing between a misplaced tap and a night's bookkeeping, and a
/// yes/no prompt is too easy to answer wrong at 2am.
/// </summary>
public sealed record DeleteTableCommand(Guid ChampionshipId, Guid TableId, string ConfirmName)
    : ICommand;

internal sealed class DeleteTableCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IChampionshipActivityNotifier notifier)
    : ICommandHandler<DeleteTableCommand>
{
    public async Task<Result> Handle(DeleteTableCommand command, CancellationToken cancellationToken)
    {
        // Admin, not TableManager. A manager runs a night; wiping one is a
        // different kind of decision.
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
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

        if (!string.Equals(command.ConfirmName.Trim(), table.Name, StringComparison.Ordinal))
        {
            return Result.Failure(TableErrors.ConfirmationDoesNotMatch);
        }

        // Explicit rather than relying on the cascade: the settlement transfers
        // point at table_players, and letting the database work out the order of
        // its own cascades is how the ledger's counterparty key deadlocked before.
        List<Guid> playerIds = await context.TablePlayers
            .Where(p => p.TableId == table.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        context.TableResults.RemoveRange(
            await context.TableResults.Where(r => r.TableId == table.Id).ToListAsync(cancellationToken));

        List<Settlement> settlements = await context.Settlements
            .Include(s => s.Transfers)
            .Where(s => s.TableId == table.Id)
            .ToListAsync(cancellationToken);

        context.Settlements.RemoveRange(settlements);

        context.FinalCounts.RemoveRange(
            await context.FinalCounts.Where(c => c.TableId == table.Id).ToListAsync(cancellationToken));

        context.LedgerEntries.RemoveRange(
            await context.LedgerEntries
                .Include(e => e.Chips)
                .Where(e => e.TableId == table.Id)
                .ToListAsync(cancellationToken));

        context.TablePlayers.RemoveRange(
            await context.TablePlayers.Where(p => playerIds.Contains(p.Id)).ToListAsync(cancellationToken));

        context.Tables.Remove(table);

        await context.SaveChangesAsync(cancellationToken);

        await notifier.NotifyAsync(command.ChampionshipId, cancellationToken);

        return Result.Success();
    }
}
