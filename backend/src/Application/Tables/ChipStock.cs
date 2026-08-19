using Domain.ChipSets;
using Domain.Tables.Services;

namespace Application.Tables;

public static class ChipStock
{
    /// <summary>
    /// What the case has left right now: everything it holds, minus every chip
    /// already issued at this table, minus any small-chip reserve.
    ///
    /// Derived from the ledger rather than kept as a running column. A stored
    /// counter can drift from the entries that explain it, and at the end of the
    /// night the entries are what has to reconcile against the physical count.
    /// </summary>
    public static IReadOnlyList<DenominationStock> Available(
        IReadOnlyList<ChipDenomination> denominations,
        IReadOnlyDictionary<Guid, int> issued,
        int smallChipReserve = 0)
    {
        ArgumentNullException.ThrowIfNull(denominations);
        ArgumentNullException.ThrowIfNull(issued);

        List<ChipDenomination> ascending = [.. denominations.OrderBy(d => d.EffectiveValue)];
        IReadOnlyList<double> profile = ChipDistributionCalculator.DefaultProfile(ascending.Count);

        Guid? smallest = ascending.Count > 0 ? ascending[0].Id : null;

        return
        [
            .. ascending.Select((denomination, index) =>
            {
                int reserve = denomination.Id == smallest ? smallChipReserve : 0;
                int available = denomination.Quantity - issued.GetValueOrDefault(denomination.Id) - reserve;

                return new DenominationStock
                {
                    DenominationId = denomination.Id,
                    EffectiveValue = denomination.EffectiveValue,
                    Available = Math.Max(available, 0),
                    ProfileShare = profile[index]
                };
            })
        ];
    }

    /// <summary>
    /// Chips issued per denomination so far, counted straight off the ledger.
    /// Entries that moved no chips out of the case — a trade between two players —
    /// carry no chip rows, so they correctly contribute nothing.
    /// </summary>
    public static Dictionary<Guid, int> IssuedByDenomination(
        IEnumerable<Domain.Tables.LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .SelectMany(entry => entry.Chips)
            .GroupBy(chip => chip.ChipDenominationId)
            .ToDictionary(group => group.Key, group => group.Sum(chip => chip.Quantity));
    }
}
