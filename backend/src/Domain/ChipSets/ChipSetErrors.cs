using SharedKernel;

namespace Domain.ChipSets;

public static class ChipSetErrors
{
    public static Error NotFound(Guid chipSetId) => Error.NotFound(
        "ChipSets.NotFound",
        $"The chip set with the Id = '{chipSetId}' was not found in this championship");

    public static readonly Error NoDenominations = Error.Problem(
        "ChipSets.NoDenominations",
        "A chip set needs at least one denomination");

    public static readonly Error DuplicateFaceValue = Error.Problem(
        "ChipSets.DuplicateFaceValue",
        "Each denomination in a chip set must have a distinct face value");

    /// <summary>
    /// The database refuses this anyway — the foreign key from a table to its
    /// chip case is Restrict, so losing the case cannot take the history of every
    /// table played with it. Checked here so the answer is this sentence rather
    /// than a 500 from the constraint violation.
    /// </summary>
    public static Error InUseByTables(int tableCount) => Error.Conflict(
        "ChipSets.InUseByTables",
        $"This chip case is used by {tableCount} table(s) and cannot be deleted. Their history depends on it");
}
