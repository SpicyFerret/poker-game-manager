using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Tables.Counting;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Domain.Tables.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Settle;

/// <summary>
/// Closes the night: works out who pays whom and what everyone scored.
///
/// Only possible once every chip that left the case has been counted back.
/// Settling against an incomplete count would make someone pay for chips nobody
/// found.
/// </summary>
public sealed record SettleTableCommand(Guid ChampionshipId, Guid TableId) : ICommand;

internal sealed class SettleTableCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SettleTableCommand>
{
    public async Task<Result> Handle(SettleTableCommand command, CancellationToken cancellationToken)
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

        if (table.Status is TableStatus.Settled or TableStatus.Closed)
        {
            return Result.Failure(TableErrors.AlreadySettled);
        }

        if (table.Status != TableStatus.Counting)
        {
            return Result.Failure(TableErrors.WrongStatus(table.Status, TableStatus.Counting));
        }

        TableReconciliation reconciliation =
            await GetReconciliationQueryHandler.BuildAsync(context, table, cancellationToken);

        if (!reconciliation.EveryoneHasCounted)
        {
            return Result.Failure(TableErrors.StillWaitingOnCounts);
        }

        if (!reconciliation.ChipsBalance)
        {
            return Result.Failure(TableErrors.CountsDoNotBalance);
        }

        Championship championship = await context.Championships
            .SingleAsync(c => c.Id == table.ChampionshipId, cancellationToken);

        // Who actually played, rather than who is not in standby: someone still
        // waiting on a manager's answer never sat down and has nothing to settle.
        List<TablePlayer> players = await context.TablePlayers
            .Where(p => p.TableId == table.Id &&
                        (p.Status == TablePlayerStatus.Playing || p.Status == TablePlayerStatus.Left))
            .ToListAsync(cancellationToken);

        List<LedgerEntry> entries = await context.LedgerEntries
            .Where(e => e.TableId == table.Id)
            .ToListAsync(cancellationToken);

        List<FinalCount> counts = await context.FinalCounts
            .Where(c => c.TableId == table.Id)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> effectiveValues = await context.ChipDenominations
            .Where(d => d.ChipSetId == table.ChipSetId)
            .ToDictionaryAsync(d => d.Id, d => d.EffectiveValue, cancellationToken);

        ILookup<Guid, LedgerEntry> entriesByPlayer = entries.ToLookup(e => e.TablePlayerId);
        ILookup<Guid, FinalCount> countsByPlayer = counts.ToLookup(c => c.TablePlayerId);

        List<PlayerNight> nights =
        [
            .. players.Select(player => new PlayerNight
            {
                TablePlayerId = player.Id,
                ChipsValue = TableResultCalculator.ChipsValue(
                    countsByPlayer[player.Id]
                        .GroupBy(c => c.ChipDenominationId)
                        .ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity)),
                    effectiveValues,
                    table.MoneyPerUnit),
                PaidIn = LedgerMath.PaidIn(entriesByPlayer[player.Id]),
                JoinedAtUtc = player.JoinedAtUtc
            })
        ];

        IReadOnlyList<PlayerResult> results =
            TableResultCalculator.Calculate(nights, championship.PointsByPosition);

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(
            [.. nights.Select(n => new PlayerBalance(n.TablePlayerId, n.Balance))]);

        DateTime now = dateTimeProvider.UtcNow;

        context.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            CreatedAtUtc = now,
            Transfers =
            [
                .. transfers.Select(t => new SettlementTransfer
                {
                    Id = Guid.NewGuid(),
                    FromPlayerId = t.FromPlayerId,
                    ToPlayerId = t.ToPlayerId,
                    Amount = t.Amount
                })
            ]
        });

        foreach (PlayerResult result in results)
        {
            context.TableResults.Add(new TableResult
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                TablePlayerId = result.TablePlayerId,
                Position = result.Position,
                Points = result.Points,
                Balance = result.Balance
            });
        }

        table.Status = TableStatus.Settled;
        table.ClosedAtUtc = now;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

}
