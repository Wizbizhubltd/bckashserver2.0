namespace BCKash.Domain.Identity;

/// <summary>
/// A single-use, short-lived email/SMS OTP challenge issued after password verification for
/// users without Google2FA enabled. The code hash lives here, server-side only — the JWT
/// challenge token handed to the client carries just this row's <see cref="Id"/>, never the
/// hash, so intercepting the token alone can't be brute-forced offline against the 6-digit
/// code space.
/// </summary>
public class LoginOtp
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The still-valid code this one was sent after (a "resend", or a repeated password-reset request).
    /// That earlier code keeps working until it expires, so an SMS that arrives late — after the user
    /// already asked for another — doesn't fail as a wrong code.
    /// </summary>
    public int? PreviousOtpId { get; set; }

    public User User { get; set; } = null!;
}
