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
    /// The mix for one stack when <paramref name="stackCount"/> of them are dealt
    /// at once, as they are when a table starts.
    ///
    /// Dealing stacks one after another from the whole case does not work here,
    /// even though it deducts correctly: the profile hands out the small chips
    /// first, so the earliest players take them all and the last player is left
    /// holding nothing under a 50 — unable to post a small blind at a table
    /// everyone paid the same to sit at. Ordering decided who got a playable
    /// stack, which is not a thing the seating order should decide.
    ///
    /// So each stack is dealt from its own equal share of the case instead. Every
    /// player gets the identical mix, and because the share is what was divided,
    /// the result is affordable <paramref name="stackCount"/> times over by
    /// construction — no denomination can be promised more times than it exists.
    ///
    /// A shortfall here means the case cannot make this many equal stacks at all,
    /// which is worth refusing: the alternative is a table that starts unfair.
    /// </summary>
    public static ChipDistribution CalculateEqualStacks(
        long targetUnits,
        IReadOnlyList<DenominationStock> stock,
        int stackCount)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (stackCount <= 1)
        {
            return Calculate(targetUnits, stock);
        }

        // Integer division on purpose: the remainder stays in the case rather
        // than going to whoever happens to be dealt first.
        List<DenominationStock> share =
        [
            .. stock.Select(d => d with { Available = d.Available / stackCount })
        ];

        return Calculate(targetUnits, share);
    }

    /// <summary>
    /// The opening deal: a stack for every player, worked out together.
    ///
    /// Equal stacks are the goal, and <see cref="CalculateEqualStacks"/> is tried
    /// first. When the case cannot be split evenly, the stacks are dealt one
    /// after another from a shrinking case instead — everyone still gets a stack
    /// of the same value, just built from different chips. A table that starts
    /// beats a table that refuses to, so unequal is only ever compared against
    /// not playing, never against equal.
    ///
    /// Only when even that cannot cover everybody is the deal refused, and then
    /// it names how short the stack it gave up on was.
    /// </summary>
    public static OpeningDeal DealOpeningStacks(
        long targetUnits,
        IReadOnlyList<DenominationStock> stock,
        int playerCount)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (playerCount <= 0)
        {
            return new OpeningDeal { ShortfallUnits = targetUnits > 0 ? targetUnits : 0 };
        }

        // Nothing to share out: one player's stack is the whole question, and
        // dividing by one would only hide the partial mix behind an empty deal.
        if (playerCount == 1)
        {
            ChipDistribution only = Calculate(targetUnits, stock);

            return only.IsComplete
                ? new OpeningDeal { Stacks = [only], IsEqual = true }
                : new OpeningDeal { ShortfallUnits = only.ShortfallUnits, Attempted = only };
        }

        ChipDistribution equal = CalculateEqualStacks(targetUnits, stock, playerCount);

        if (equal.IsComplete)
        {
            return new OpeningDeal
            {
                Stacks = [.. Enumerable.Repeat(equal, playerCount)],
                IsEqual = true
            };
        }

        var remaining = stock.ToDictionary(d => d.DenominationId, d => d.Available);
        var stacks = new List<ChipDistribution>(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            List<DenominationStock> current =
            [
                .. stock.Select(d => d with { Available = remaining[d.DenominationId] })
            ];

            ChipDistribution stack = Calculate(targetUnits, current);

            if (!stack.IsComplete)
            {
                // All or nothing: a table where some players were dealt in and
                // others were not is worse than one that has not started. The
                // partial mix goes back with it so the screen can show how close
                // the case got rather than only how short it fell.
                return new OpeningDeal
                {
                    ShortfallUnits = stack.ShortfallUnits,
                    Attempted = stack
                };
            }

            foreach (ChipCount chip in stack.Chips)
            {
                remaining[chip.DenominationId] -= chip.Quantity;
            }

            stacks.Add(stack);
        }

        return new OpeningDeal { Stacks = stacks, IsEqual = false };
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
