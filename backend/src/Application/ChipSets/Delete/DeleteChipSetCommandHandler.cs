using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Domain.ChipSets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ChipSets.Delete;

internal sealed class DeleteChipSetCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<DeleteChipSetCommand>
{
    public async Task<Result> Handle(DeleteChipSetCommand command, CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        ChipSet? chipSet = await context.ChipSets.SingleOrDefaultAsync(
            s => s.Id == command.ChipSetId && s.ChampionshipId == command.ChampionshipId,
            cancellationToken);

        if (chipSet is null)
        {
            return Result.Failure(ChipSetErrors.NotFound(command.ChipSetId));
        }

        context.ChipSets.Remove(chipSet);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
