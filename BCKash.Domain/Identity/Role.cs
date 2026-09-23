namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `roles` table. `LegacyPermissionsRaw` is the original free-text
/// `permissions` column, kept for the one-time RBAC migration parser — new code
/// must use <see cref="RolePermissions"/> instead (FRD §1.5).
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public bool TimeLimit { get; set; }
    public string? FromTime { get; set; }
    public string? ToTime { get; set; }
    public string? AccessDays { get; set; }

    public string? LegacyPermissionsRaw { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RoleUser> RoleUsers { get; set; } = new List<RoleUser>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
