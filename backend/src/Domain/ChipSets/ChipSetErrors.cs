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
}
