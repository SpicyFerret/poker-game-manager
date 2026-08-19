using Application.Abstractions.Messaging;

namespace Application.Championships.GetMine;

public sealed record GetMyChampionshipsQuery : IQuery<IReadOnlyList<ChampionshipSummaryResponse>>;
