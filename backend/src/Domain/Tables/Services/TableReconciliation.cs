namespace Domain.Tables.Services;

public sealed record DenominationReconciliation
{
    public Guid DenominationId { get; init; }
    public int FaceValue { get; init; }
    public int EffectiveValue { get; init; }

    /// <summary>How many of this chip left the case at this table.</summary>
    public int Issued { get; init; }

    /// <summary>How many players say they are holding.</summary>
    public int Counted { get; init; }

    /// <summary>
    /// Counted minus issued. Positive means chips appeared from nowhere — usually
    /// someone counting a different case's chips. Negative means chips are missing:
    /// still on the floor, in a pocket, or someone counted short.
    /// </summary>
    public int Difference => Counted - Issued;
}

public sealed record TableReconciliation
{
    public IReadOnlyList<DenominationReconciliation> Denominations { get; init; } = [];

    /// <summary>Players still expected to report what they are holding.</summary>
    public IReadOnlyList<Guid> AwaitingCountFrom { get; init; } = [];

    public bool EveryoneHasCounted => AwaitingCountFrom.Count == 0;

    public bool ChipsBalance => Denominations.All(d => d.Difference == 0);

    /// <summary>
    /// The gate. Money cannot be settled until every chip that left the case is
    /// accounted for — settling against an incomplete count means someone pays for
    /// chips nobody found.
    /// </summary>
    public bool CanReconcile => EveryoneHasCounted && ChipsBalance;
}

public static class TableReconciliationService
{
    public static TableReconciliation Build(
        IReadOnlyList<(Guid Id, int FaceValue, int EffectiveValue)> denominations,
        IReadOnlyDictionary<Guid, int> issued,
        IReadOnlyDictionary<Guid, int> counted,
        IReadOnlyList<Guid> playersExpectedToCount,
        IReadOnlyList<Guid> playersWhoHaveCounted)
    {
        ArgumentNullException.ThrowIfNull(denominations);
        ArgumentNullException.ThrowIfNull(issued);
        ArgumentNullException.ThrowIfNull(counted);
        ArgumentNullException.ThrowIfNull(playersExpectedToCount);
        ArgumentNullException.ThrowIfNull(playersWhoHaveCounted);

        var reported = playersWhoHaveCounted.ToHashSet();

        return new TableReconciliation
        {
            Denominations =
            [
                .. denominations
                    .OrderBy(d => d.EffectiveValue)
                    .Select(d => new DenominationReconciliation
                    {
                        DenominationId = d.Id,
                        FaceValue = d.FaceValue,
                        EffectiveValue = d.EffectiveValue,
                        Issued = issued.GetValueOrDefault(d.Id),
                        Counted = counted.GetValueOrDefault(d.Id)
                    })
            ],
            AwaitingCountFrom = [.. playersExpectedToCount.Where(id => !reported.Contains(id))]
        };
    }
}
