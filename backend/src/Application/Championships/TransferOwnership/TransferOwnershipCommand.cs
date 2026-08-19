using Application.Abstractions.Messaging;

namespace Application.Championships.TransferOwnership;

public sealed record TransferOwnershipCommand(Guid ChampionshipId, Guid NewOwnerId) : ICommand;
