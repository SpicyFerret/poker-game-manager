using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Create;

internal sealed class CreateTableCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTableCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTableCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        Championship? championship = await context.Championships
            .SingleOrDefaultAsync(c => c.Id == command.ChampionshipId, cancellationToken);

        if (championship is null)
        {
            return Result.Failure<Guid>(ChampionshipErrors.NotFound(command.ChampionshipId));
        }

        bool chipSetBelongsHere = await context.ChipSets.AnyAsync(
            s => s.Id == command.ChipSetId && s.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (!chipSetBelongsHere)
        {
            return Result.Failure<Guid>(TableErrors.ChipSetBelongsToAnotherChampionship);
        }

        bool hasChips = await context.ChipDenominations.AnyAsync(
            d => d.ChipSetId == command.ChipSetId && d.Quantity > 0,
            cancellationToken);

        if (!hasChips)
        {
            return Result.Failure<Guid>(TableErrors.ChipSetEmpty);
        }

        // The championship can pin the stakes for the year, or merely suggest them.
        decimal buyIn = championship.EnforceDefaults
            ? championship.DefaultBuyIn
            : command.BuyIn ?? championship.DefaultBuyIn;

        decimal rebuy = championship.EnforceDefaults
            ? championship.DefaultRebuy
            : command.Rebuy ?? championship.DefaultRebuy;

        var table = new PokerTable
        {
            Id = Guid.NewGuid(),
            ChampionshipId = command.ChampionshipId,
            ChipSetId = command.ChipSetId,
            CreatedBy = userContext.UserId,
            Name = command.Name.Trim(),
            Status = TableStatus.Open,
            BuyIn = buyIn,
            Rebuy = rebuy,
            // Snapshotted, not read through the championship later: a table played
            // last month has to keep the rate it was played at.
            MoneyPerUnit = championship.MoneyPerUnit,
            JoinPolicy = command.JoinPolicy,
            AllowLateEntry = command.AllowLateEntry,
            JoinCode = command.JoinPolicy == JoinPolicy.Code ? await GenerateJoinCodeAsync(cancellationToken) : null,
            SmallChipReserve = command.SmallChipReserve,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        context.Tables.Add(table);

        await context.SaveChangesAsync(cancellationToken);

        return table.Id;
    }

    private async Task<string> GenerateJoinCodeAsync(CancellationToken cancellationToken)
    {
        // Shares the invite alphabet, for the same reason: these get read aloud.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = InviteCode.Generate();

            if (!await context.Tables.AnyAsync(t => t.JoinCode == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return InviteCode.Generate();
    }
}
