namespace Domain.ChipSets;

/// <summary>
/// A physical case of chips belonging to a championship. Stock is tracked per
/// denomination so a table can hand out balanced stacks and know what is left.
/// </summary>
public sealed class ChipSet
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<ChipDenomination> Denominations { get; set; } = [];
}
