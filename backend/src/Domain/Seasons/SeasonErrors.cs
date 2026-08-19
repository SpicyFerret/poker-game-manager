using SharedKernel;

namespace Domain.Seasons;

public static class SeasonErrors
{
    public static Error NotFound(Guid seasonId) => Error.NotFound(
        "Seasons.NotFound",
        $"The season with the Id = '{seasonId}' was not found in this championship");

    public static readonly Error EndsBeforeItStarts = Error.Problem(
        "Seasons.EndsBeforeItStarts",
        "A season cannot end before it starts");

    /// <summary>
    /// Overlapping seasons would make a table's result count towards two rankings
    /// at once, so the ranking totals would stop adding up.
    /// </summary>
    public static readonly Error Overlaps = Error.Conflict(
        "Seasons.Overlaps",
        "That date range overlaps an existing season in this championship");
}
