using Domain.Users;

namespace Domain.Tables;

public enum TablePlayerStatus
{
    /// <summary>Sat down, waiting for the manager to start. No chips yet.</summary>
    Standby = 0,

    /// <summary>In the game, holding chips.</summary>
    Playing = 1,

    /// <summary>Walked away. Still owed a final count and a settlement.</summary>
    Left = 2,

    /// <summary>
    /// Asked to join a table already in play, waiting on a manager. Not at the
    /// table yet in any sense that counts: no chips, no stack owed, and nothing
    /// for the reconciliation to expect from them.
    /// </summary>
    Requested = 3
}

public sealed class TablePlayer
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public Guid UserId { get; set; }
    public TablePlayerStatus Status { get; set; }

    /// <summary>Order people sat down in, which is the seating order in the UI.</summary>
    public int SeatOrder { get; set; }

    public DateTime JoinedAtUtc { get; set; }

    public User User { get; set; }
}
