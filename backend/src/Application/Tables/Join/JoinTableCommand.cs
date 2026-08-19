using Application.Abstractions.Messaging;

namespace Application.Tables.Join;

/// <summary>Sitting down at a table yourself. Code is only needed for a coded table.</summary>
public sealed record JoinTableCommand(Guid ChampionshipId, Guid TableId, string? Code) : ICommand;
