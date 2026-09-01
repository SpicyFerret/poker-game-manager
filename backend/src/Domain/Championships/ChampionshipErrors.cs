using SharedKernel;

namespace Domain.Championships;

public static class ChampionshipErrors
{
    public static Error NotFound(Guid championshipId) => Error.NotFound(
        "Championships.NotFound",
        $"The championship with the Id = '{championshipId}' was not found");

    /// <summary>
    /// NotFound rather than Forbidden on purpose, and worded to cover both: a
    /// non-member must not be able to tell an id they cannot see from one that
    /// does not exist.
    /// </summary>
    public static readonly Error NotAMember = Error.NotFound(
        "Championships.NotAMember",
        "The championship was not found, or you are not a member of it");

    public static Error InsufficientRole(ChampionshipRole required) => Error.Forbidden(
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
    public static readonly Error CannotActOnEqualOrHigherRole = Error.Forbidden(
        "Championships.CannotActOnEqualOrHigherRole",
        "You can only manage members whose role is strictly below your own, and only up to the role below yours");

    // The next three are bad requests, not permission problems: the caller is
    // allowed to do this, they just asked for something that makes no sense.
    public static readonly Error OwnerRoleIsTransferredNotAssigned = Error.Problem(
        "Championships.OwnerRoleIsTransferredNotAssigned",
        "Ownership is handed over with the transfer action, not by assigning the Owner role");

    public static readonly Error NewOwnerMustBeAdmin = Error.Problem(
        "Championships.NewOwnerMustBeAdmin",
        "Ownership can only be transferred to an existing Admin of this championship");

    public static readonly Error CannotRemoveOwner = Error.Conflict(
        "Championships.CannotRemoveOwner",
        "The owner cannot be removed. Transfer ownership first");

    public static readonly Error ConfirmationDoesNotMatch = Error.Problem(
        "Championships.ConfirmationDoesNotMatch",
        "Type the championship's name exactly to confirm");

    /// <summary>
    /// The reordered list has to be exactly the caller's own memberships — one
    /// missing or unknown id would either silently leave a championship stuck
    /// wherever it was, or move something that is not the caller's to move.
    /// </summary>
    public static readonly Error ReorderMustIncludeEveryChampionship = Error.Problem(
        "Championships.ReorderMustIncludeEveryChampionship",
        "The list must include every championship you belong to, exactly once, and nothing else");
}
