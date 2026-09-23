namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `permissions` table — a tree (`parent_id`, legacy default 0 for
/// root, mapped here to null). One policy per <see cref="Slug"/> is registered in
/// ASP.NET Core's authorization system (FRD §17).
/// </summary>
public class Permission
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }

    public Permission? Parent { get; set; }
    public ICollection<Permission> Children { get; set; } = new List<Permission>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
