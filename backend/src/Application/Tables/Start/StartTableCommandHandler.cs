using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Start;

internal sealed class StartTableCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<StartTableCommand>
{
    public async Task<Result> Handle(StartTableCommand command, CancellationToken cancellationToken)
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

        if (table.Status != TableStatus.Open)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Open));
        }

        List<TablePlayer> players = await context.TablePlayers
            .Where(p => p.TableId == table.Id && p.Status == TablePlayerStatus.Standby)
            .OrderBy(p => p.SeatOrder)
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return Result.Failure(TableErrors.NoPlayers);
        }

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        // Nothing has been issued yet, but going through the same path as a rebuy
        // keeps one definition of "what is left in the case".
        List<LedgerEntry> existing = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> issued = ChipStock.IssuedByDenomination(existing);

        var entries = new List<LedgerEntry>();

        foreach (TablePlayer player in players)
        {
            Result<LedgerEntry> issue = StackIssuer.Issue(
                table,
                player,
                LedgerEntryType.BuyIn,
                table.BuyIn,
                table.BuyInUnits,
                denominations,
                issued,
                userContext.UserId,
                dateTimeProvider.UtcNow);

            if (issue.IsFailure)
            {
                // All or nothing. Starting with some players dealt in and others
                // not would leave a table nobody can reconcile, so the manager
                // gets told how short the case is and decides what to change.
                return Result.Failure(issue.Error);
            }

            entries.Add(issue.Value);
            player.Status = TablePlayerStatus.Playing;
        }

        context.LedgerEntries.AddRange(entries);

        table.Status = TableStatus.Running;
        table.StartedAtUtc = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
