namespace Domain.Tables.Services;

public sealed record PlayerBalance(Guid TablePlayerId, decimal Balance);

public sealed record SettlementTransferPlan(Guid FromPlayerId, Guid ToPlayerId, decimal Amount);

/// <summary>
/// Works out who pays whom, in as few payments as possible.
///
/// Minimising payments in general is NP-hard, but a poker table is small, so for
/// up to <see cref="ExactLimit"/> players this finds the true minimum rather than
/// something merely decent. Everyone at the table is paying by phone at 2am, so
/// each payment removed is a real saving.
/// </summary>
public static class SettlementCalculator
{
    /// <summary>
    /// Above this the exact search is skipped for a greedy pass. The exact search
    /// enumerates submasks, which costs 3^n: fine at 15 (~14M cheap steps, once,
    /// at the end of a night), unreasonable beyond.
    /// </summary>
    public const int ExactLimit = 15;

    public static IReadOnlyList<SettlementTransferPlan> Calculate(IReadOnlyList<PlayerBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        // Cents, not decimal, throughout the search. Money that has to add up
        // exactly has no business going near fractional arithmetic.
        List<(Guid Id, long Cents)> owed =
        [
            .. balances
                .Select(b => (b.TablePlayerId, Cents: ToCents(b.Balance)))
                .Where(b => b.Cents != 0)
        ];

        if (owed.Count == 0)
        {
            return [];
        }

        return owed.Count <= ExactLimit
            ? SettleExactly(owed)
            : SettleGreedily(owed);
    }

    /// <summary>
    /// Splits the table into the largest possible number of groups that each
    /// settle among themselves, then clears each group. A group of k people needs
    /// exactly k-1 payments, so more groups means fewer payments — and finding
    /// the most groups is the whole problem.
    /// </summary>
    private static List<SettlementTransferPlan> SettleExactly(List<(Guid Id, long Cents)> owed)
    {
        int n = owed.Count;
        int combinations = 1 << n;

        long[] sums = new long[combinations];

        for (int mask = 1; mask < combinations; mask++)
        {
            int lowest = System.Numerics.BitOperations.TrailingZeroCount(mask);
            sums[mask] = sums[mask & (mask - 1)] + owed[lowest].Cents;
        }

        // parts[mask] = most self-settling groups mask can be split into, or -1
        // when it cannot be split at all.
        int[] parts = new int[combinations];
        int[] chosen = new int[combinations];
        Array.Fill(parts, -1);
        parts[0] = 0;

        for (int mask = 1; mask < combinations; mask++)
        {
            int lowestBit = mask & -mask;

            // Every group must contain the lowest remaining player, which stops
            // the same partition being counted once per ordering.
            for (int sub = mask; sub != 0; sub = (sub - 1) & mask)
            {
                if ((sub & lowestBit) == 0 || sums[sub] != 0)
                {
                    continue;
                }

                int rest = parts[mask ^ sub];

                if (rest >= 0 && rest + 1 > parts[mask])
                {
                    parts[mask] = rest + 1;
                    chosen[mask] = sub;
                }
            }
        }

        int full = combinations - 1;

        // Only reachable if the balances do not sum to zero, which the caller is
        // supposed to have established. Falling back beats throwing at 2am.
        if (parts[full] < 0)
        {
            return SettleGreedily(owed);
        }

        var transfers = new List<SettlementTransferPlan>();

        for (int mask = full; mask != 0;)
        {
            int group = chosen[mask];

            transfers.AddRange(ClearGroup([.. Members(group, owed)]));

            mask ^= group;
        }

        return transfers;
    }

    private static IEnumerable<(Guid Id, long Cents)> Members(int mask, List<(Guid Id, long Cents)> owed)
    {
        for (int i = 0; i < owed.Count; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                yield return owed[i];
            }
        }
    }

    /// <summary>
    /// Clears one group that already sums to zero. Each payment zeroes at least
    /// one person, and the last zeroes two, so k people take exactly k-1 payments
    /// — which is the minimum for a group that cannot be split further.
    /// </summary>
    private static List<SettlementTransferPlan> ClearGroup(List<(Guid Id, long Cents)> group) =>
        SettleGreedily(group);

    private static List<SettlementTransferPlan> SettleGreedily(List<(Guid Id, long Cents)> owed)
    {
        // Biggest debt against biggest credit each time: it zeroes the largest
        // amount per payment, so the count stays low.
        var debtors = new PriorityQueue<Guid, long>();
        var creditors = new PriorityQueue<Guid, long>();
        var remaining = new Dictionary<Guid, long>();

        foreach ((Guid id, long cents) in owed)
        {
            remaining[id] = cents;

            if (cents < 0)
            {
                debtors.Enqueue(id, cents);
            }
            else
            {
                creditors.Enqueue(id, -cents);
            }
        }

        var transfers = new List<SettlementTransferPlan>();

        while (debtors.Count > 0 && creditors.Count > 0)
        {
            Guid debtor = debtors.Dequeue();
            Guid creditor = creditors.Dequeue();

            long amount = Math.Min(-remaining[debtor], remaining[creditor]);

            if (amount > 0)
            {
                transfers.Add(new SettlementTransferPlan(debtor, creditor, FromCents(amount)));

                remaining[debtor] += amount;
                remaining[creditor] -= amount;
            }

            if (remaining[debtor] < 0)
            {
                debtors.Enqueue(debtor, remaining[debtor]);
            }

            if (remaining[creditor] > 0)
            {
                creditors.Enqueue(creditor, -remaining[creditor]);
            }
        }

        return transfers;
    }

    private static long ToCents(decimal money) => (long)decimal.Round(money * 100m, MidpointRounding.AwayFromZero);

    private static decimal FromCents(long cents) => cents / 100m;
}
