using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Join;

internal sealed class JoinTableCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<JoinTableCommand>
{
    public async Task<Result> Handle(JoinTableCommand command, CancellationToken cancellationToken)
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

        Result gate = CheckJoinable(table, command.Code, caller.Value);

        if (gate.IsFailure)
        {
            return gate;
        }

        bool alreadyHere = await context.TablePlayers.AnyAsync(
            p => p.TableId == table.Id && p.UserId == userContext.UserId,
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
            UserId = userContext.UserId,
            // Standby whether or not play has started: sitting down is not the same
            // as being dealt in, and chips only move when a manager says so.
            Status = TablePlayerStatus.Standby,
            SeatOrder = seats,
            JoinedAtUtc = dateTimeProvider.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Result CheckJoinable(PokerTable table, string? code, ChampionshipRole callerRole)
    {
        if (table.Status == TableStatus.Open)
        {
            // fine
        }
        else if (table.Status == TableStatus.Running)
        {
            if (!table.AllowLateEntry)
            {
                return Result.Failure(TableErrors.LateEntryNotAllowed);
            }
        }
        else
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Open));
        }

        switch (table.JoinPolicy)
        {
            case JoinPolicy.AnyMember:
                return Result.Success();

            case JoinPolicy.Code:
                string normalised = InviteCode.Normalize(code ?? string.Empty);

                return normalised.Length > 0 && normalised == table.JoinCode
                    ? Result.Success()
                    : Result.Failure(TableErrors.WrongJoinCode);

            case JoinPolicy.InviteOnly:
            default:
                // A manager can still seat themselves; everyone else has to be added.
                return callerRole >= ChampionshipRole.TableManager
                    ? Result.Success()
                    : Result.Failure(TableErrors.JoinRefused);
        }
    }
}
