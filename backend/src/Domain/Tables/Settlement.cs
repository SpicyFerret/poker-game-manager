namespace Domain.Tables;

/// <summary>
/// Who pays whom, worked out once when the table reconciled. Immutable
/// afterwards: people start sending money the moment it appears, so recomputing
/// it later would contradict payments already made.
/// </summary>
public sealed class Settlement
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<SettlementTransfer> Transfers { get; set; } = [];
}

public sealed class SettlementTransfer
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Guid FromPlayerId { get; set; }
    public Guid ToPlayerId { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// One player's night, frozen at close. The championship rankings are sums over these
/// rows — nothing is recomputed from the ledger to show a ranking.
/// </summary>
public sealed class TableResult
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public Guid TablePlayerId { get; set; }
    public int Position { get; set; }
    public int Points { get; set; }
    public decimal Balance { get; set; }
}
