using SharedKernel;

namespace Domain.Championships;

public sealed class Championship : Entity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Suggested buy-in and rebuy for tables in this championship.
    /// </summary>
    public decimal DefaultBuyIn { get; set; }
    public decimal DefaultRebuy { get; set; }

    /// <summary>
    /// When true a table may not depart from the defaults above. Groups differ:
    /// some agree an amount once for the year, others settle it per night.
    /// </summary>
    public bool EnforceDefaults { get; set; }

    /// <summary>
    /// How much real money one chip unit is worth (e.g. 0.05 → a 1000-unit stack
    /// is R$ 50). Money is never stored on the chip itself, so the same case can
    /// be played for different stakes.
    /// </summary>
    public decimal MoneyPerUnit { get; set; }

    /// <summary>
    /// Points awarded by finishing position: index 0 is first place. A position
    /// past the end of the list scores nothing, so the list also decides how deep
    /// the scoring goes.
    /// </summary>
    public List<int> PointsByPosition { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}
