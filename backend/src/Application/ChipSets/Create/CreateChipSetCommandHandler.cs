using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using SharedKernel;

namespace Application.ChipSets.Create;

internal sealed class CreateChipSetCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateChipSetCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateChipSetCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        if (command.Denominations.Count == 0)
        {
            return Result.Failure<Guid>(ChipSetErrors.NoDenominations);
        }

        if (command.Denominations.Select(d => d.FaceValue).Distinct().Count() != command.Denominations.Count)
        {
            return Result.Failure<Guid>(ChipSetErrors.DuplicateFaceValue);
        }

        var chipSet = new ChipSet
        {
            Id = Guid.NewGuid(),
            ChampionshipId = command.ChampionshipId,
            Name = command.Name.Trim(),
            CreatedAtUtc = dateTimeProvider.UtcNow,
            Denominations = [.. command.Denominations.Select(d => new ChipDenomination
            {
                Id = Guid.NewGuid(),
                FaceValue = d.FaceValue,
                EffectiveValue = d.EffectiveValue,
                Quantity = d.Quantity,
                Colour = string.IsNullOrWhiteSpace(d.Colour) ? null : d.Colour.Trim()
            })]
        };

        context.ChipSets.Add(chipSet);

        await context.SaveChangesAsync(cancellationToken);

        return chipSet.Id;
    }
}
