namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `throttle` table — a row is written per failed login attempt
/// (FR-SEC-2); the login throttling service counts rows within a rolling window per
/// `UserId`/`Ip` to decide lockout.
/// </summary>
public class Throttle
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Ip { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
}
