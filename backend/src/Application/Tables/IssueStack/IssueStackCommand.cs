using Application.Abstractions.Messaging;

namespace Application.Tables.IssueStack;

/// <summary>
/// Hands one player a stack from the case, during play: a rebuy for someone
/// already in, or the opening stack for a late entrant still in standby.
///
/// One command for both because the only difference is the amount and the label —
/// the chips come off the same case by the same arithmetic.
/// </summary>
public sealed record IssueStackCommand(Guid ChampionshipId, Guid TableId, Guid TablePlayerId, bool IsRebuy)
    : ICommand;
