using BCKash.Application.Auth;
using BCKash.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BCKash.Api.Authorization;

/// <summary>Named (non-permission) policies. Permission policies are built on the fly by <see cref="PermissionPolicyProvider"/>.</summary>
public static class AuthPolicies
{
    /// <summary>Only users whose user_type is super_admin — for data only super admins may change (zones, cities).</summary>
    public const string SuperAdmin = "SuperAdmin";

    public static void AddBCKashPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SuperAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaimTypes.UserType, UserTypeSlugs.SuperAdmin));
    }
}
