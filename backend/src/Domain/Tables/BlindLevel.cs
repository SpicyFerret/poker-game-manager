namespace Domain.Tables;

/// <summary>
/// One rung of a table's blind ladder. Levels belong to the table rather than to
/// a reusable structure on the championship: a night's blinds get argued over and
/// adjusted at the table, and a shared structure would mean editing one night
/// silently rewrote the others.
/// </summary>
public sealed class BlindLevel
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }

    /// <summary>1-based, in the order they are played.</summary>
    public int Order { get; set; }

    public int SmallBlind { get; set; }
    public int BigBlind { get; set; }
    public int Ante { get; set; }

    /// <summary>How long this level lasts. Zero means it never advances on its own.</summary>
    public int DurationSeconds { get; set; }
}
