using SharedKernel;

namespace Domain.Championships;

public static class ChampionshipErrors
{
    public static Error NotFound(Guid championshipId) => Error.NotFound(
        "Championships.NotFound",
        $"The championship with the Id = '{championshipId}' was not found");

    public static readonly Error NotAMember = Error.NotFound(
        "Championships.NotAMember",
        "The championship was not found, or you are not a member of it");

    public static Error InsufficientRole(ChampionshipRole required) => Error.Failure(
        "Championships.InsufficientRole",
        $"This action requires the {required} role or higher in this championship");

    public static readonly Error MemberNotFound = Error.NotFound(
        "Championships.MemberNotFound",
        "That person is not a member of this championship");

    public static readonly Error AlreadyAMember = Error.Conflict(
        "Championships.AlreadyAMember",
        "That person is already a member of this championship");

    /// <summary>
    /// Covers both directions of the same rule: you can neither grant a role at
    /// or above your own, nor act on a member who already holds one.
    /// </summary>
    public static readonly Error CannotActOnEqualOrHigherRole = Error.Failure(
        "Championships.CannotActOnEqualOrHigherRole",
        "You can only manage members whose role is strictly below your own, and only up to the role below yours");

    public static readonly Error OwnerRoleIsTransferredNotAssigned = Error.Failure(
        "Championships.OwnerRoleIsTransferredNotAssigned",
        "Ownership is handed over with the transfer action, not by assigning the Owner role");

    public static readonly Error NewOwnerMustBeAdmin = Error.Failure(
        "Championships.NewOwnerMustBeAdmin",
        "Ownership can only be transferred to an existing Admin of this championship");

    public static readonly Error CannotRemoveOwner = Error.Failure(
        "Championships.CannotRemoveOwner",
        "The owner cannot be removed. Transfer ownership first");
}
