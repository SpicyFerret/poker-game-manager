using Application.Abstractions.Messaging;

namespace Application.ChipSets.Create;

public sealed record CreateChipSetCommand(
    Guid ChampionshipId,
    string Name,
    IReadOnlyList<ChipDenominationModel> Denominations)
    : ICommand<Guid>;
