using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ChipSets.Update;

internal sealed class UpdateChipSetCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<UpdateChipSetCommand>
{
    public async Task<Result> Handle(UpdateChipSetCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (command.Denominations.Select(d => d.FaceValue).Distinct().Count() != command.Denominations.Count)
        {
            return Result.Failure(ChipSetErrors.DuplicateFaceValue);
        }

        ChipSet? chipSet = await context.ChipSets
            .Include(s => s.Denominations)
            .SingleOrDefaultAsync(
                s => s.Id == command.ChipSetId && s.ChampionshipId == command.ChampionshipId,
                cancellationToken);

        if (chipSet is null)
        {
            return Result.Failure(ChipSetErrors.NotFound(command.ChipSetId));
        }

        chipSet.Name = command.Name.Trim();

        // Matched by face value rather than replaced wholesale, so a denomination
        // that survives an edit keeps its id. From Phase 2 the ledger records
        // chips issued per denomination id, and swapping ids under it would
        // orphan the history of every table already played with this case.
        var existing = chipSet.Denominations.ToDictionary(d => d.FaceValue);

        foreach (ChipDenominationModel model in command.Denominations)
        {
            string? colour = string.IsNullOrWhiteSpace(model.Colour) ? null : model.Colour.Trim();

            if (existing.TryGetValue(model.FaceValue, out ChipDenomination? denomination))
            {
                denomination.EffectiveValue = model.EffectiveValue;
                denomination.Quantity = model.Quantity;
                denomination.Colour = colour;

                existing.Remove(model.FaceValue);
            }
            else
            {
                // Added through the DbSet rather than the navigation collection.
                // The key is a Guid we assign ourselves, and an entity that turns
                // up in a tracked graph with its key already populated is taken to
                // be an existing row: EF marks it Modified and the save fails with
                // "attempted to update an entity that does not exist".
                context.ChipDenominations.Add(new ChipDenomination
                {
                    Id = Guid.NewGuid(),
                    ChipSetId = chipSet.Id,
                    FaceValue = model.FaceValue,
                    EffectiveValue = model.EffectiveValue,
                    Quantity = model.Quantity,
                    Colour = colour
                });
            }
        }

        // Whatever is left was not in the new list, so it has been dropped.
        // Removed through the DbSet too, so the intent is a delete rather than a
        // severed relationship the provider has to interpret.
        context.ChipDenominations.RemoveRange(existing.Values);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
