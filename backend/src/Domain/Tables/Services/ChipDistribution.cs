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
