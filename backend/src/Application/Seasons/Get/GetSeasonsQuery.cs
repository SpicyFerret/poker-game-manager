using Application.Abstractions.Messaging;

namespace Application.Seasons.Get;

public sealed record GetSeasonsQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<SeasonResponse>>;
