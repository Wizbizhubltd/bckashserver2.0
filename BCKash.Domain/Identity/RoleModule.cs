namespace BCKash.Domain.Identity;

/// <summary>One office-portal module (see <see cref="OfficePortalModules"/>) a super admin has ticked for a staff role.</summary>
public class RoleModule
{
    public int RoleId { get; set; }
    public string Module { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
}
