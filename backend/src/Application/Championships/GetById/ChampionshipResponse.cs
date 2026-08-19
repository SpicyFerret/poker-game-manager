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
}
