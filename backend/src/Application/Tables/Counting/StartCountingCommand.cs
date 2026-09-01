using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Realtime;
using Domain.Championships;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Counting;

/// <summary>Play is over; everyone starts counting what they are holding.</summary>
public sealed record StartCountingCommand(Guid ChampionshipId, Guid TableId) : ICommand;

internal sealed class StartCountingCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IChampionshipActivityNotifier notifier)
    : ICommandHandler<StartCountingCommand>
{
    public async Task<Result> Handle(StartCountingCommand command, CancellationToken cancellationToken)
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

        if (table.Status != TableStatus.Running)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Running));
        }

        table.Status = TableStatus.Counting;

        await context.SaveChangesAsync(cancellationToken);

        await notifier.NotifyAsync(command.ChampionshipId, cancellationToken);

        return Result.Success();
    }
}
