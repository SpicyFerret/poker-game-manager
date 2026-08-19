using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

/// <summary>
/// Global, permission-name based authorization inherited from the template.
///
/// It currently grants any authenticated caller, because this app's real
/// permissions are scoped to a championship — the same person is Owner in one
/// and Player in another — so a global permission set cannot express them. See
/// docs/domain.md, "Authorization"; the championship-scoped check arrives with
/// the championship aggregate and takes over these routes.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // An unauthenticated caller has no permissions. This has to be explicit:
        // the previous version fell through to GetUserId(), which throws on a
        // principal with no subject claim, turning an expired token into a 500
        // instead of a 401.
        if (context.User is { Identity.IsAuthenticated: true })
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
