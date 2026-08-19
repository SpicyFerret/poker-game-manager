using Application.Abstractions.Messaging;

namespace Application.Tables.BuyChips;

/// <summary>
/// The case is empty and someone still wants to rebuy, so they buy chips off a
/// player who has plenty.
///
/// Recorded as two entries and no chip movement out of the case: the buyer pays,
/// the seller is credited the same amount, and the chips involved were already in
/// play. Total chips at the table is unchanged, so the end-of-night count still
/// reconciles against what the case issued — and the seller is not out of pocket
/// for having bailed the table out.
/// </summary>
public sealed record BuyChipsFromPlayerCommand(
    Guid ChampionshipId,
    Guid TableId,
    Guid BuyerPlayerId,
    Guid SellerPlayerId,
    decimal Amount)
    : ICommand;
