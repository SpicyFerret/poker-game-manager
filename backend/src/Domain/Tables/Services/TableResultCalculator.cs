namespace Domain.Tables.Services;

public sealed record PlayerNight
{
    public Guid TablePlayerId { get; init; }

    /// <summary>Money value of the chips they finished holding.</summary>
    public decimal ChipsValue { get; init; }

    /// <summary>Buy-ins, rebuys and chips bought, less anything credited for chips sold.</summary>
    public decimal PaidIn { get; init; }

    /// <summary>Tie-break of last resort: whoever sat down first placed higher.</summary>
    public DateTime JoinedAtUtc { get; init; }

    public decimal Balance => ChipsValue - PaidIn;
}

public sealed record PlayerResult
{
    public Guid TablePlayerId { get; init; }
    public int Position { get; init; }
    public decimal Balance { get; init; }
    public int Points { get; init; }
}

public static class TableResultCalculator
{
    /// <summary>
    /// Places everyone by what they walked away with, and awards the
    /// championship's points for that placing.
    ///
    /// Balance, not chip count: someone up R$ 20 on one buy-in beat someone up
    /// R$ 10 after three rebuys, even if the second is holding more chips. What
    /// people actually argue about is who came out ahead.
    /// </summary>
    public static IReadOnlyList<PlayerResult> Calculate(
        IReadOnlyList<PlayerNight> players,
        IReadOnlyList<int> pointsByPosition)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(pointsByPosition);

        List<PlayerNight> ranked =
        [
            .. players
                .OrderByDescending(p => p.Balance)
                // Same balance: fewer chips bought is the better night, since they
                // risked less to get there. Then simply who arrived first, so the
                // order is never arbitrary.
                .ThenBy(p => p.PaidIn)
                .ThenBy(p => p.JoinedAtUtc)
        ];

        var results = new List<PlayerResult>(ranked.Count);

        for (int index = 0; index < ranked.Count; index++)
        {
            PlayerNight player = ranked[index];

            results.Add(new PlayerResult
            {
                TablePlayerId = player.TablePlayerId,
                Position = index + 1,
                Balance = player.Balance,
                // Past the end of the table, a placing simply scores nothing —
                // which is also how the owner decides how deep scoring goes.
                Points = index < pointsByPosition.Count ? pointsByPosition[index] : 0
            });
        }

        return results;
    }

    /// <summary>
    /// Money value of a counted stack. Effective value, never the printed one:
    /// treating a 5 chip as 100 is exactly what the override exists for.
    /// </summary>
    public static decimal ChipsValue(
        IReadOnlyDictionary<Guid, int> countsByDenomination,
        IReadOnlyDictionary<Guid, int> effectiveValues,
        decimal moneyPerUnit)
    {
        ArgumentNullException.ThrowIfNull(countsByDenomination);
        ArgumentNullException.ThrowIfNull(effectiveValues);

        long units = countsByDenomination.Sum(
            pair => (long)pair.Value * effectiveValues.GetValueOrDefault(pair.Key));

        return units * moneyPerUnit;
    }
}
