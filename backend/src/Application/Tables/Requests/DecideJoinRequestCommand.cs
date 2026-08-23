using Application.Abstractions.Messaging;

namespace Application.Tables.Requests;

/// <summary>
/// A manager answering someone who asked to join a table already in play.
/// Approving seats them in standby, exactly where a normal join would have put
/// them; turning them away removes the request entirely, so asking again is a
/// fresh question rather than an argument with a stored "no".
/// </summary>
public sealed record DecideJoinRequestCommand(
    Guid ChampionshipId,
    Guid TableId,
    Guid TablePlayerId,
    bool Approved) : ICommand;
