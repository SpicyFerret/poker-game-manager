namespace Domain.Seasons;

/// <summary>
/// The window a ranking covers. Without one, "accumulates over the year" has
/// nothing to reset against and every ranking would run since the beginning.
/// </summary>
public sealed class Season
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// Inclusive start, exclusive end. Null end means the season is still open.
    /// </summary>
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
}
