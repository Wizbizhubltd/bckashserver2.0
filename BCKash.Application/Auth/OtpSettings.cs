namespace BCKash.Application.Auth;

/// <summary>Config for the emailed/SMS'd one-time codes (login and password reset).</summary>
public class OtpSettings
{
    /// <summary>The flat `MASTER_OTP` environment variable (see .env.example).</summary>
    public const string MasterOtpKey = "MASTER_OTP";

    /// <summary>
    /// When set, this code is accepted in place of the generated one on every OTP check —
    /// for testing and support logins where SMS/email delivery isn't available. The generated
    /// code is still sent and still works. Leave unset in production.
    /// </summary>
    public string? MasterOtp { get; set; }
}
