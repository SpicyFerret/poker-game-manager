namespace Application.Championships;

public static class ChampionshipDefaults
{
    /// <summary>
    /// Used when a championship is created without a points table. A gentle curve
    /// rather than winner-take-all: home games are the same handful of people all
    /// year, and a scheme that only pays first place stops meaning anything by
    /// about March. The owner can replace it at any time.
    /// </summary>
    public static readonly int[] PointsByPosition = [10, 7, 5, 3, 2, 1];
}
