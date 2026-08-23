using Application.Abstractions.Messaging;

namespace Application.Tables.RemovePlayer;

/// <summary>
/// A manager taking someone back off the table — the mirror of adding them by
/// hand, for the person who joined the wrong table or is not going to play
/// after all.
/// </summary>
public sealed record RemovePlayerCommand(Guid ChampionshipId, Guid TableId, Guid TablePlayerId)
    : ICommand;
