namespace Domain.Tables;

/// <summary>
/// The shared blind clock. Optional: a table without blind levels simply has no
/// clock, which is how most casual nights run.
///
/// Stores timestamps, never a counting-down number. Each phone works out the
/// remaining time from these, so the clock reads the same on every device and
/// polling does not have to be punctual for it to be right — the one thing a
/// countdown stored server-side could never manage.
/// </summary>
public sealed class TableClock
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }

    /// <summary>1-based index into the table's levels.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>When the current level began, ignoring any time spent paused.</summary>
    public DateTime LevelStartedAtUtc { get; set; }

    /// <summary>Set while paused; null while running.</summary>
    public DateTime? PausedAtUtc { get; set; }

    /// <summary>Total time already spent paused within the current level.</summary>
    public int AccumulatedPauseSeconds { get; set; }

    public bool IsPaused => PausedAtUtc is not null;

    /// <summary>
    /// Seconds elapsed in the current level. While paused the clock stands still;
    /// while running, whatever was paused earlier is discounted.
    /// </summary>
    public int ElapsedSeconds(DateTime utcNow)
    {
        DateTime upTo = PausedAtUtc ?? utcNow;
        double raw = (upTo - LevelStartedAtUtc).TotalSeconds - AccumulatedPauseSeconds;

        return (int)Math.Max(raw, 0);
    }
}
