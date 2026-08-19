using Application.Abstractions.Messaging;

namespace Application.Tables.Start;

public sealed record StartTableCommand(Guid ChampionshipId, Guid TableId) : ICommand;
