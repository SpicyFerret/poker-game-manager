using Domain.Users;

namespace Domain.Tables;

public enum TablePlayerStatus
{
    /// <summary>Sat down, waiting for the manager to start. No chips yet.</summary>
    Standby = 0,

    /// <summary>In the game, holding chips.</summary>
    Playing = 1,

    /// <summary>Walked away. Still owed a final count and a settlement.</summary>
    Left = 2
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
