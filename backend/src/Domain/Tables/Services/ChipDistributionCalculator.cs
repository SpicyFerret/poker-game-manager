namespace Domain.Tables.Services;

/// <summary>
/// Works out which chips make up one stack, given what is left in the case.
///
/// Two passes. First a profile pass hands out roughly the intended mix — mostly
/// small chips early on, so people can actually bet. Then a greedy pass from the
/// largest denomination downwards covers whatever rounding and stock limits left
/// over, so the stack lands on the target exactly.
///
/// Because it always reads current stock, the behaviour everyone expects falls
/// out with no special rule: the small chips run out over the night, and later
/// stacks simply come in bigger denominations.
/// </summary>
public static class ChipDistributionCalculator
{
    public static ChipDistribution Calculate(long targetUnits, IReadOnlyList<DenominationStock> stock)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (targetUnits <= 0 || stock.Count == 0)
        {
            return new ChipDistribution { ShortfallUnits = targetUnits > 0 ? targetUnits : 0 };
        }

        // Ascending, so "smallest" and "largest" below mean what they say.
        List<DenominationStock> ascending = [.. stock
            .Where(d => d.EffectiveValue > 0 && d.Available > 0)
            .OrderBy(d => d.EffectiveValue)];

        if (ascending.Count == 0)
        {
            return new ChipDistribution { ShortfallUnits = targetUnits };
        }

        var taken = new Dictionary<Guid, int>();
        long remaining = targetUnits;

        // Pass 1 — the intended mix. Floor throughout: overshooting the target is
        // not an option, so the leftovers are dealt with in pass 2.
        foreach (DenominationStock denomination in ascending)
        {
            if (denomination.ProfileShare <= 0)
            {
                continue;
            }

            long wantedUnits = (long)(targetUnits * denomination.ProfileShare);
            int wanted = (int)Math.Min(wantedUnits / denomination.EffectiveValue, denomination.Available);
            wanted = (int)Math.Min(wanted, remaining / denomination.EffectiveValue);

            if (wanted <= 0)
            {
                continue;
            }

            taken[denomination.DenominationId] = wanted;
            remaining -= (long)wanted * denomination.EffectiveValue;
        }

        // Pass 2 — close the gap with the biggest chips that still fit, which
        // keeps the leftover small chips in the case for the next rebuy.
        foreach (DenominationStock denomination in Enumerable.Reverse(ascending))
        {
            if (remaining <= 0)
            {
                break;
            }

            int alreadyTaken = taken.GetValueOrDefault(denomination.DenominationId);
            int spare = denomination.Available - alreadyTaken;

            if (spare <= 0)
            {
                continue;
            }

            int extra = (int)Math.Min(remaining / denomination.EffectiveValue, spare);

            if (extra <= 0)
            {
                continue;
            }

            taken[denomination.DenominationId] = alreadyTaken + extra;
            remaining -= (long)extra * denomination.EffectiveValue;
        }

        // Whatever is left is genuinely unreachable with this case: either the
        // stock ran out, or the target is not a multiple of anything left in it
        // (a 3-unit gap with nothing smaller than a 5). Reported rather than
        // rounded away, because a stack that is short by even one unit puts the
        // table's reconciliation out by that much at the end of the night.
        return new ChipDistribution
        {
            Chips = [.. taken
                .Where(pair => pair.Value > 0)
                .Select(pair => new ChipCount(pair.Key, pair.Value))],
            AllocatedUnits = targetUnits - remaining,
            ShortfallUnits = remaining
        };
    }

    /// <summary>
    /// Default profile when a case has none: a linear ramp over the denominations
    /// sorted ascending, so the largest chip carries the most value and the
    /// smallest the least. For four denominations that is 10/20/30/40 percent.
    ///
    /// A ramp rather than an even split because an even split buries a stack in
    /// small chips, and rather than a heavy top because a stack of only big chips
    /// cannot post a small blind.
    /// </summary>
    public static IReadOnlyList<double> DefaultProfile(int denominationCount)
    {
        if (denominationCount <= 0)
        {
            return [];
        }

        double total = denominationCount * (denominationCount + 1) / 2.0;

        return [.. Enumerable.Range(1, denominationCount).Select(rank => rank / total)];
    }
}
