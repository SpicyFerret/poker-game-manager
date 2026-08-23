namespace Domain.Tables;

/// <summary>
/// One night of poker. Named PokerTable rather than Table because "Table" collides
/// with too much — EF, data grids, and every reader's assumptions.
/// </summary>
public sealed class PokerTable
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public Guid ChipSetId { get; set; }
    public Guid CreatedBy { get; set; }

    public string Name { get; set; }
    public TableStatus Status { get; set; }

    /// <summary>
    /// Copied from the championship when the table opens rather than read through
    /// it later. A table played last month must keep the numbers it was played
    /// with, whatever the championship's defaults have become since.
    /// </summary>
    public decimal BuyIn { get; set; }
    public decimal Rebuy { get; set; }
    public decimal MoneyPerUnit { get; set; }

    public JoinPolicy JoinPolicy { get; set; }

    /// <summary>What happens when someone tries to sit down after play has started.</summary>
    public LateEntryPolicy LateEntry { get; set; }

    /// <summary>Set only when <see cref="JoinPolicy"/> is Code.</summary>
    public string? JoinCode { get; set; }

    /// <summary>
    /// Smallest-denomination chips held back from the opening stacks so the first
    /// rebuys still have something to bet with. The chips everyone runs out of
    /// first are the ones the game needs most.
    /// </summary>
    public int SmallChipReserve { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>Buy-in expressed in chip units, which is what the case deals in.</summary>
    public long BuyInUnits => ToUnits(BuyIn);

    public long RebuyUnits => ToUnits(Rebuy);

    private long ToUnits(decimal money) =>
        MoneyPerUnit <= 0 ? 0 : (long)decimal.Round(money / MoneyPerUnit, MidpointRounding.AwayFromZero);
}
