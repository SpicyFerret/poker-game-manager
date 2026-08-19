namespace Domain.Tables;

/// <summary>
/// A table's life, in order. The gate is <see cref="Reconciled"/>: it is only
/// reachable once every chip handed out has been counted back, and only from
/// there can money be settled.
/// </summary>
public enum TableStatus
{
    /// <summary>Being set up. Nobody can join yet.</summary>
    Draft = 0,

    /// <summary>Accepting players, who wait in standby. No chips issued.</summary>
    Open = 1,

    /// <summary>Started. Stacks dealt, rebuys allowed.</summary>
    Running = 2,

    /// <summary>Play is over; players report what they are holding.</summary>
    Counting = 3,

    /// <summary>Counts match what left the case. Ready to settle.</summary>
    Reconciled = 4,

    /// <summary>Who pays whom has been worked out.</summary>
    Settled = 5,

    /// <summary>Done. Results counted towards the season.</summary>
    Closed = 6,

    /// <summary>Called off. Anything issued is void.</summary>
    Cancelled = 7
}
