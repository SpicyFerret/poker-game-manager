using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.AddPlayer;

internal sealed class AddPlayerCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddPlayerCommand>
{
    public async Task<Result> Handle(AddPlayerCommand command, CancellationToken cancellationToken)
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

        // Same window a self-join gets — a manager adding someone is not a way
        // around the table being closed for new players.
        if (table.Status == TableStatus.Running)
        {
            // A manager seating someone by hand *is* the approval, so Request
            // needs no second step here — only Blocked closes the door.
            if (table.LateEntry == LateEntryPolicy.Blocked)
            {
                return Result.Failure(TableErrors.LateEntryNotAllowed);
            }
        }
        else if (table.Status != TableStatus.Open)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Open));
        }

        bool isMember = await context.ChampionshipMembers.AnyAsync(
            m => m.ChampionshipId == command.ChampionshipId && m.UserId == command.UserId,
            cancellationToken);

        if (!isMember)
        {
            return Result.Failure(ChampionshipErrors.MemberNotFound);
        }

        bool alreadyHere = await context.TablePlayers.AnyAsync(
            p => p.TableId == table.Id && p.UserId == command.UserId,
            cancellationToken);

        if (alreadyHere)
        {
            return Result.Failure(TableErrors.AlreadyAtTheTable);
        }

        int seats = await context.TablePlayers.CountAsync(p => p.TableId == table.Id, cancellationToken);

        context.TablePlayers.Add(new TablePlayer
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            UserId = command.UserId,
            Status = TablePlayerStatus.Standby,
            SeatOrder = seats,
            JoinedAtUtc = dateTimeProvider.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
