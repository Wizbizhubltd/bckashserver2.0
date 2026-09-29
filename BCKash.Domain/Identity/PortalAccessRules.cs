namespace BCKash.Domain.Identity;

/// <summary>
/// Which frontend a user_type may sign in to. The control portal is for super admins only; the
/// office portal is for every other user_type and never for a super admin.
/// </summary>
public static class PortalAccessRules
{
    public const string ControlPortal = "control";
    public const string OfficePortal = "office";

    /// <summary>True when <paramref name="portal"/> is unset (a client that doesn't say) or the user_type belongs on it.</summary>
    public static bool CanSignIn(string? userTypeSlug, string? portal)
    {
        if (string.IsNullOrWhiteSpace(portal))
        {
            return true;
        }

        var isSuperAdmin = userTypeSlug == UserTypeSlugs.SuperAdmin;
        return portal.Trim().ToLowerInvariant() switch
        {
            ControlPortal => isSuperAdmin,
            OfficePortal => !isSuperAdmin && userTypeSlug is not null,
            _ => false,
        };
    }
}
