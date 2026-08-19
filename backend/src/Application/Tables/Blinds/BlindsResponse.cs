namespace Application.Tables.Blinds;

public sealed record BlindLevelResponse
{
    public int Order { get; init; }
    public int SmallBlind { get; init; }
    public int BigBlind { get; init; }
    public int Ante { get; init; }
    public int DurationSeconds { get; init; }
}

/// <summary>
/// The clock as a client needs it. Sends elapsed time and the server's own
/// instant rather than a countdown: each phone works out the remainder itself, so
/// every device agrees and a late poll does not make the clock wrong.
/// </summary>
public sealed record TableClockResponse
{
    public int CurrentLevel { get; init; }
    public bool IsPaused { get; init; }
    public int ElapsedSeconds { get; init; }

    /// <summary>
    /// When the server produced this. A client compares it against its own clock
    /// to keep ticking between polls without drifting away from everyone else.
    /// </summary>
    public DateTime ServerTimeUtc { get; init; }
}

public sealed record BlindsResponse
{
    public IReadOnlyList<BlindLevelResponse> Levels { get; init; } = [];

    /// <summary>Null when the table has no clock, which is the common case.</summary>
    public TableClockResponse? Clock { get; init; }
}
