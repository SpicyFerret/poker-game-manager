using Domain.ChipSets;
using Domain.Tables;
using Domain.Tables.Services;
using SharedKernel;

namespace Application.Tables;

/// <summary>
/// Turns "this player needs a stack worth X" into a ledger entry with the actual
/// chips on it, or a failure naming the gap.
///
/// Shared by starting a table (a stack each, all at once) and by a single buy-in
/// or rebuy during play, so both deduct from the same running stock in the same
/// way. Splitting them would be two chances to get the arithmetic wrong.
/// </summary>
internal static class StackIssuer
{
    public static Result<LedgerEntry> Issue(
        PokerTable table,
        TablePlayer player,
        LedgerEntryType type,
        decimal money,
        long targetUnits,
        IReadOnlyList<ChipDenomination> denominations,
        Dictionary<Guid, int> issuedSoFar,
        Guid createdBy,
        DateTime nowUtc)
    {
        // The reserve only holds chips back from the opening deal; once play has
        // started, holding them back is the opposite of the point.
        int reserve = type == LedgerEntryType.BuyIn ? table.SmallChipReserve : 0;

        IReadOnlyList<DenominationStock> stock = ChipStock.Available(denominations, issuedSoFar, reserve);

        ChipDistribution distribution = ChipDistributionCalculator.Calculate(targetUnits, stock);

        if (!distribution.IsComplete)
        {
            // Refused outright rather than dealing what fits. A stack short by any
            // amount puts the night's reconciliation out by exactly that much, and
            // nobody would notice until the count at the end.
            return Result.Failure<LedgerEntry>(TableErrors.NotEnoughChips(distribution.ShortfallUnits));
        }

        LedgerEntry entry = BuildEntry(table, player, type, money, distribution, createdBy, nowUtc);

        // Caller keeps issuing against the same map, so a bulk deal sees each
        // stack come off the case before working out the next.
        foreach (ChipCount chip in distribution.Chips)
        {
            issuedSoFar[chip.DenominationId] = issuedSoFar.GetValueOrDefault(chip.DenominationId) + chip.Quantity;
        }

        return entry;
    }

    /// <summary>
    /// The ledger entry for a mix that has already been worked out.
    ///
    /// Separate from <see cref="Issue"/> because the opening deal computes one
    /// mix for the whole table and hands every player that same one — there is
    /// nothing left to calculate per player, and recalculating would reintroduce
    /// exactly the ordering effect that made stacks unequal.
    /// </summary>
    public static LedgerEntry BuildEntry(
        PokerTable table,
        TablePlayer player,
        LedgerEntryType type,
        decimal money,
        ChipDistribution distribution,
        Guid createdBy,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(distribution);

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            TablePlayerId = player.Id,
            Type = type,
            MoneyAmount = money,
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc,
            Chips =
            [
                .. distribution.Chips.Select(chip => new LedgerEntryChip
                {
                    Id = Guid.NewGuid(),
                    ChipDenominationId = chip.DenominationId,
                    Quantity = chip.Quantity
                })
            ]
        };
    }
}
