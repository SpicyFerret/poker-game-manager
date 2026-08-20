using Domain.Championships;

namespace Application.Championships.GetById;

public sealed record ChampionshipResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public Guid OwnerId { get; init; }

    public decimal DefaultBuyIn { get; init; }

    public decimal DefaultRebuy { get; init; }

    public bool EnforceDefaults { get; init; }

    public decimal MoneyPerUnit { get; init; }

    public IReadOnlyList<int> PointsByPosition { get; init; } = [];

    /// <summary>The caller's role, so the UI knows what to offer.</summary>
    public ChampionshipRole Role { get; init; }

    /// <summary>
    /// Who is top of the points ranking. Null until a table has been settled.
    /// Shown on the card because "who is winning" is the question people open
    /// this screen already holding.
    /// </summary>
    public string? LeaderDisplayName { get; init; }

    public int LeaderPoints { get; init; }
}
