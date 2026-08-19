using Application.Abstractions.Messaging;

namespace Application.Championships.Join;

public sealed record JoinByCodeCommand(string Code) : ICommand<JoinByCodeResponse>;

public sealed record JoinByCodeResponse(Guid ChampionshipId, string Name);
