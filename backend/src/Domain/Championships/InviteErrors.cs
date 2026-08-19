using SharedKernel;

namespace Domain.Championships;

public static class InviteErrors
{
    /// <summary>
    /// Deliberately the same error for "no such code", "expired", "used up" and
    /// "revoked". Telling the difference would let someone probe for valid codes.
    /// </summary>
    public static readonly Error NotUsable = Error.NotFound(
        "Invites.NotUsable",
        "That invite code is not valid. It may have expired, been used up, or been revoked");

    public static readonly Error NotFound = Error.NotFound(
        "Invites.NotFound",
        "The invite was not found in this championship");

    public static readonly Error MaxUsesMustBePositive = Error.Problem(
        "Invites.MaxUsesMustBePositive",
        "The maximum number of uses must be greater than zero when set");
}
