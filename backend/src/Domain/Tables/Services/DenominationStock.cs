namespace Domain.Tables.Services;

/// <summary>
/// One denomination as the distribution calculator sees it: what a chip counts
/// as in play, how many are actually left in the case right now, and how much of
/// a stack it should ideally carry.
/// </summary>
public sealed record DenominationStock
{
    public Guid DenominationId { get; init; }

    /// <summary>What the chip counts as in play, never its printed value.</summary>
    public int EffectiveValue { get; init; }

    /// <summary>How many are left in the case, after anything already handed out.</summary>
    public int Available { get; init; }

    /// <summary>
    /// Share of the stack's total value this denomination should ideally carry,
    /// 0..1. The shares across a case are expected to sum to roughly 1; they are
    /// a target, not a constraint, and stock always wins.
    /// </summary>
    public double ProfileShare { get; init; }
}
