using Application.Abstractions.Messaging;

namespace Application.Championships.Invites.Revoke;

public sealed record RevokeInviteCommand(Guid ChampionshipId, Guid InviteId) : ICommand;
