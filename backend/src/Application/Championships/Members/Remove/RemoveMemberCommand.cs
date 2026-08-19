using Application.Abstractions.Messaging;

namespace Application.Championships.Members.Remove;

public sealed record RemoveMemberCommand(Guid ChampionshipId, Guid UserId) : ICommand;
