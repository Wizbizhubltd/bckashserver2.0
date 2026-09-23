namespace BCKash.Domain.Identity;

/// <summary>
/// New additive join table (FRD §1.5 RBAC redesign) for direct per-user permission
/// overrides, replacing the legacy free-text `users.permissions` column. Populated by
/// the one-time migration parser from <see cref="User.LegacyPermissionsRaw"/>.
/// A user's effective permission set is role permissions ∪ this table (FR-SEC-4).
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public int PermissionId { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
