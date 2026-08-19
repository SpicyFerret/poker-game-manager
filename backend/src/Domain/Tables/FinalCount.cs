namespace Domain.Tables;

/// <summary>
/// What one player says they are holding of one chip, at the end of the night.
/// Self-reported, because that is how it already works at the table: everyone
/// counts their own stack and calls it out.
/// </summary>
public sealed class FinalCount
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public Guid TablePlayerId { get; set; }
    public Guid ChipDenominationId { get; set; }
    public int Quantity { get; set; }
    public DateTime ReportedAtUtc { get; set; }
}
