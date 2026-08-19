using Application.Abstractions.Messaging;

namespace Application.Seasons.Create;

public sealed record CreateSeasonCommand(
    Guid ChampionshipId,
    string Name,
    DateOnly StartsOn,
    DateOnly? EndsOn)
    : ICommand<Guid>;
