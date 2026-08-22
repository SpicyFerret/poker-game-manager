namespace Domain.Tables.Services;

public sealed record ChipCount(Guid DenominationId, int Quantity);

public sealed record ChipDistribution
{
    public IReadOnlyList<ChipCount> Chips { get; init; } = [];

    public long AllocatedUnits { get; init; }

    /// <summary>
    /// Units the case could not cover. Non-zero means the stack was not handed
    /// out — the caller has to say so rather than quietly dealing a short stack,
    /// which would put the table's books out by exactly this much.
    /// </summary>
    public long ShortfallUnits { get; init; }

    public bool IsComplete => ShortfallUnits == 0;
}

/// <summary>
/// The whole opening deal: one stack per player, worked out together rather than
/// one at a time, because what the case can give the fifth player depends on what
/// it already gave the first four.
/// </summary>
public sealed record OpeningDeal
{
    /// <summary>One stack per player, in the order the players were handed in.</summary>
    public IReadOnlyList<ChipDistribution> Stacks { get; init; } = [];

    /// <summary>
    /// Whether everyone got the identical mix. False means the case could not be
    /// split evenly and the stacks were dealt one after another instead — same
    /// value each, different chips. Worth telling the manager, since it changes
    /// what they count out of the case.
    /// </summary>
    public bool IsEqual { get; init; }

    /// <summary>Units missing from the first stack the case could not cover.</summary>
    public long ShortfallUnits { get; init; }

    /// <summary>
    /// How far the stack that fell short actually got, when the deal was
    /// refused. Empty on a deal that went through. Kept because "plenty of chips"
    /// and "can deal this stack" are different questions, and someone told only
    /// the second goes hunting through the case for a gap the preview could have
    /// shown them.
    /// </summary>
    public ChipDistribution Attempted { get; init; } = new();

    public bool IsComplete => Stacks.Count > 0 && ShortfallUnits == 0;
}
