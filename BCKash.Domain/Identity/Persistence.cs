namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `persistences` table (originally Cartalyst Sentinel's "remember me"
/// store). Repurposed as the refresh-token store for the new JWT flow — `Code` holds
/// the refresh token's hash, per the Phase 0 plan (reuses the existing table rather
/// than adding a new one).
/// </summary>
public class Persistence
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>The sign-in session this refresh token belongs to — see <see cref="User.ActiveSessionId"/>.</summary>
    public string? SessionId { get; set; }

    /// <summary>The client-generated id of the device that signed in.</summary>
    public string? DeviceId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
