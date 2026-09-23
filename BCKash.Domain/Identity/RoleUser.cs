namespace BCKash.Domain.Identity;

/// <summary>Maps the legacy `role_users` join table exactly — no schema change here.</summary>
public class RoleUser
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
