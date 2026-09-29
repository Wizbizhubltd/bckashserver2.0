namespace BCKash.Domain.Identity;

/// <summary>
/// A zone a director oversees. Directors are the only user_type assigned zones — a super admin
/// assigns them — and they manage every office in those zones. Everyone else below a super admin
/// works in the single office on their user record.
/// </summary>
public class UserZone
{
    public int UserId { get; set; }
    public int ZoneId { get; set; }
    public int? AssignedById { get; set; }
    public DateTime? CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Organization.Zone Zone { get; set; } = null!;
}
