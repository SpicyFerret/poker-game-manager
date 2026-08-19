using Application.Abstractions.Messaging;

namespace Application.ChipSets.Update;

public sealed record UpdateChipSetCommand(
    Guid ChampionshipId,
    Guid ChipSetId,
    string Name,
    IReadOnlyList<ChipDenominationModel> Denominations)
    : ICommand;
