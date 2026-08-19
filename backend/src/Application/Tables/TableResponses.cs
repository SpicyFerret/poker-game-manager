using Domain.Tables;

namespace Application.Tables;

public sealed record TableSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public TableStatus Status { get; init; }
    public decimal BuyIn { get; init; }
    public int PlayerCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
}

public sealed record TablePlayerResponse
{
    public Guid TablePlayerId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; }
    public TablePlayerStatus Status { get; init; }
    public int SeatOrder { get; init; }

    /// <summary>
    /// What this player is down at the moment: buy-ins, rebuys and chips bought
    /// off others, less anything credited for chips they sold. This is the figure
    /// their final count gets measured against.
    /// </summary>
    public decimal PaidIn { get; init; }

    public int RebuyCount { get; init; }

    /// <summary>Whether the settlement will be able to say where to send their money.</summary>
    public bool HasPaymentHandle { get; init; }
}

public sealed record ChipStockResponse
{
    public Guid DenominationId { get; init; }
    public int FaceValue { get; init; }
    public int EffectiveValue { get; init; }

    /// <summary>Still in the case: what it holds, less everything issued here.</summary>
    public int Remaining { get; init; }

    public int Issued { get; init; }
}

public sealed record TableDetailResponse
{
    public Guid Id { get; init; }
    public Guid ChampionshipId { get; init; }
    public string Name { get; init; }
    public TableStatus Status { get; init; }

    public decimal BuyIn { get; init; }
    public decimal Rebuy { get; init; }
    public decimal MoneyPerUnit { get; init; }
    public long BuyInUnits { get; init; }

    public JoinPolicy JoinPolicy { get; init; }
    public bool AllowLateEntry { get; init; }

    /// <summary>
    /// Only filled in for someone who can manage the table. It is what lets a
    /// person sit down, so a plain player has no reason to read it back.
    /// </summary>
    public string? JoinCode { get; init; }

    public int SmallChipReserve { get; init; }
    public DateTime? StartedAtUtc { get; init; }

    public IReadOnlyList<TablePlayerResponse> Players { get; init; } = [];
    public IReadOnlyList<ChipStockResponse> Stock { get; init; } = [];

    /// <summary>Money on the table: everything paid in by everyone.</summary>
    public decimal TotalPaidIn { get; init; }

    /// <summary>Whether the caller may start, deal, or record a trade here.</summary>
    public bool CanManage { get; init; }

    /// <summary>The caller's own seat, when they are at this table.</summary>
    public Guid? MyPlayerId { get; init; }
}
