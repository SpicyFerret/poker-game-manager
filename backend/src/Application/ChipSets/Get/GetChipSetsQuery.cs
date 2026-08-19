using Application.Abstractions.Messaging;

namespace Application.ChipSets.Get;

public sealed record GetChipSetsQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<ChipSetResponse>>;
