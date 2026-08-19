using Application.Abstractions.Messaging;

namespace Application.Championships.GetById;

public sealed record GetChampionshipByIdQuery(Guid ChampionshipId) : IQuery<ChampionshipResponse>;
