using Application.Abstractions.Messaging;

namespace Application.Tables.AddPlayer;

/// <summary>
/// A manager seating someone else, rather than that person joining themselves.
/// The only way in on an <c>InviteOnly</c> table, and useful on any table for
/// someone who is not looking at their phone right now.
/// </summary>
public sealed record AddPlayerCommand(Guid ChampionshipId, Guid TableId, Guid UserId) : ICommand;
