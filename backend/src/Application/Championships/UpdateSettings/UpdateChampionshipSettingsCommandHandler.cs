using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.UpdateSettings;

internal sealed class UpdateChampionshipSettingsCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : ICommandHandler<UpdateChampionshipSettingsCommand>
{
    public async Task<Result> Handle(
        UpdateChampionshipSettingsCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> role = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.Admin,
            cancellationToken);

        if (role.IsFailure)
        {
            return Result.Failure(role.Error);
        }

        Championship? championship = await context.Championships
            .SingleOrDefaultAsync(c => c.Id == command.ChampionshipId, cancellationToken);

        if (championship is null)
        {
            return Result.Failure(ChampionshipErrors.NotFound(command.ChampionshipId));
        }

        championship.Name = command.Name.Trim();
        championship.Description = string.IsNullOrWhiteSpace(command.Description)
            ? null
            : command.Description.Trim();
        championship.DefaultBuyIn = command.DefaultBuyIn;
        championship.DefaultRebuy = command.DefaultRebuy;
        championship.EnforceDefaults = command.EnforceDefaults;
        championship.MoneyPerUnit = command.MoneyPerUnit;
        championship.PointsByPosition = [.. command.PointsByPosition];

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
