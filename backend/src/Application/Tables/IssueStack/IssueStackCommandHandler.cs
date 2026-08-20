using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.IssueStack;

internal sealed class IssueStackCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<IssueStackCommand>
{
    public async Task<Result> Handle(IssueStackCommand command, CancellationToken cancellationToken)
    {
        // Player, not TableManager: a rebuy for yourself needs nobody's
        // permission but your own. Dealing someone in, or rebuying for someone
        // else, is checked explicitly below once we know who "someone" is.
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

        if (table.Status != TableStatus.Running)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Running));
        }

        TablePlayer? player = await context.TablePlayers.SingleOrDefaultAsync(
            p => p.Id == command.TablePlayerId && p.TableId == table.Id,
            cancellationToken);

        if (player is null)
        {
            return Result.Failure(TableErrors.NotAPlayer);
        }

        bool isOwnStack = player.UserId == userContext.UserId;

        if (command.IsRebuy)
        {
            if (!isOwnStack && caller.Value < ChampionshipRole.TableManager)
            {
                return Result.Failure(TableErrors.CannotRebuySomeoneElse);
            }
        }
        else if (caller.Value < ChampionshipRole.TableManager)
        {
            return Result.Failure(TableErrors.CannotDealInSomeoneElse);
        }

        // A rebuy tops up someone already in; a buy-in deals in someone who is
        // still waiting. Neither applies to a player who has walked away.
        if (command.IsRebuy && player.Status != TablePlayerStatus.Playing)
        {
            return Result.Failure(TableErrors.PlayerNotPlaying);
        }

        if (!command.IsRebuy && player.Status != TablePlayerStatus.Standby)
        {
            return Result.Failure(TableErrors.AlreadyAtTheTable);
        }

        List<ChipDenomination> denominations = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToListAsync(cancellationToken);

        List<LedgerEntry> existing = await context.LedgerEntries
            .Include(e => e.Chips)
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> issued = ChipStock.IssuedByDenomination(existing);

        Result<LedgerEntry> issue = StackIssuer.Issue(
            table,
            player,
            command.IsRebuy ? LedgerEntryType.Rebuy : LedgerEntryType.BuyIn,
            command.IsRebuy ? table.Rebuy : table.BuyIn,
            command.IsRebuy ? table.RebuyUnits : table.BuyInUnits,
            denominations,
            issued,
            userContext.UserId,
            dateTimeProvider.UtcNow);

        if (issue.IsFailure)
        {
            // The case is dry. The way out is buying chips off another player,
            // which is a different command and moves no chips out of the case.
            return Result.Failure(issue.Error);
        }

        context.LedgerEntries.Add(issue.Value);
        player.Status = TablePlayerStatus.Playing;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
