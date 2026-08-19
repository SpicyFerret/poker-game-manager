namespace Application.ChipSets;

public sealed record ChipSetResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<ChipDenominationResponse> Denominations { get; init; } = [];

    /// <summary>
    /// Everything the case holds, counted in play units. Handy before a table
    /// starts: it caps how many stacks can be dealt out at a given buy-in.
    /// </summary>
    public long TotalUnits { get; init; }
}

public sealed record ChipDenominationResponse
{
    public Guid Id { get; init; }

    public int FaceValue { get; init; }

    public int EffectiveValue { get; init; }

    public int Quantity { get; init; }

    public string? Colour { get; init; }
}
