using Microsoft.AspNetCore.Authorization;

namespace BCKash.Api.Authorization;

/// <summary>One requirement per `permissions.slug` (FRD §17) — "does the caller's token carry this permission claim".</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
