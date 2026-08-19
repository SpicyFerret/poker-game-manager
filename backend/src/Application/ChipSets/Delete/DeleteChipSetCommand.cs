using Application.Abstractions.Messaging;

namespace Application.ChipSets.Delete;

public sealed record DeleteChipSetCommand(Guid ChampionshipId, Guid ChipSetId) : ICommand;
