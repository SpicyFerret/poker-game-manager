namespace Domain.ChipSets;

public sealed class ChipDenomination
{
    public Guid Id { get; set; }
    public Guid ChipSetId { get; set; }

    /// <summary>
    /// What is printed on the chip. Only ever used to describe it to a player —
    /// no arithmetic uses this value.
    /// </summary>
    public int FaceValue { get; set; }

    /// <summary>
    /// What the chip counts as in play, and the only value the maths uses. Lets a
    /// case of 5/25/50/100 chips be played as 100/500/1000/2000 without anyone
    /// relabelling anything, which is the usual workaround when a case is too
    /// small for the stakes.
    /// </summary>
    public int EffectiveValue { get; set; }

    /// <summary>
    /// How many of this chip the case holds in total.
    /// </summary>
    public int Quantity { get; set; }

    public string? Colour { get; set; }
}
