namespace BCKash.Domain.Identity;

/// <summary>
/// New additive join table (FRD §1.5 RBAC redesign) replacing the legacy free-text
/// `roles.permissions` column with a proper many-to-many relation. Populated by the
/// one-time migration parser from <see cref="Role.LegacyPermissionsRaw"/>.
/// </summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
