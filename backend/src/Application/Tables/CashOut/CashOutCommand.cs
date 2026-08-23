using Application.Abstractions.Messaging;
using Application.Tables.Counting;

namespace Application.Tables.CashOut;

/// <summary>
/// Someone going home before the night ends. They count what is in front of
/// them, hand it back, and take the money for it there and then.
///
/// The counts are per denomination rather than a single total, for the same
/// reason the end-of-night count is: those chips go back into the case and get
/// dealt to somebody else, and the reconciliation has to know which ones.
/// </summary>
public sealed record CashOutCommand(
    Guid ChampionshipId,
    Guid TableId,
    Guid TablePlayerId,
    IReadOnlyList<ChipCountEntry> Counts) : ICommand;
