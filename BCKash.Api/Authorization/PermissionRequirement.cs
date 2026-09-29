using Microsoft.AspNetCore.Authorization;

namespace BCKash.Api.Authorization;

/// <summary>
/// One requirement per `permissions.slug` (FRD §17) — "does the caller's token carry this permission
/// claim". Alternatives are separated by "|" ("Permission:a|b"): any one of them is enough.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public IReadOnlyList<string> AnyOf { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
        AnyOf = permission.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
