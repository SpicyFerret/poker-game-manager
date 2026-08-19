using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using SharedKernel;

namespace Application.Championships.Create;

internal sealed class CreateChampionshipCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateChampionshipCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateChampionshipCommand command,
        CancellationToken cancellationToken)
    {
        var championship = new Championship
        {
            Id = Guid.NewGuid(),
            OwnerId = userContext.UserId,
            Name = command.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            DefaultBuyIn = command.DefaultBuyIn,
            DefaultRebuy = command.DefaultRebuy,
            EnforceDefaults = command.EnforceDefaults,
            MoneyPerUnit = command.MoneyPerUnit,
            PointsByPosition = command.PointsByPosition is { Count: > 0 }
                ? [.. command.PointsByPosition]
                : [.. ChampionshipDefaults.PointsByPosition],
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        context.Championships.Add(championship);

        // The creator is a member as well as the owner. Everything else resolves
        // permissions through membership, so an owner without one would be locked
        // out of their own championship.
        context.ChampionshipMembers.Add(new ChampionshipMember
        {
            Id = Guid.NewGuid(),
            ChampionshipId = championship.Id,
            UserId = userContext.UserId,
            Role = ChampionshipRole.Owner,
            JoinedAtUtc = dateTimeProvider.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return championship.Id;
    }
}
