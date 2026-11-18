namespace BCKash.Infrastructure.Communications;

/// <summary>
/// Config for the system-level OTP SMS sender (distinct from the per-office, DB-configured
/// <c>SmsGateway</c> rows used for client campaigns — see <c>HttpSmsGatewaySender</c>). Bound
/// from the "Sms" section; the API key is a secret, supplied via `dotnet user-secrets set
/// Sms:ApiKey "..."`, same pattern as `Email:SmtpPassword`.
/// </summary>
public class SmsSettings
{
    public const string SectionName = "Sms";

    /// <summary>The provider's send-SMS endpoint URL (e.g. Termii's `https://v4.api.termii.com/api/sms/send`).</summary>
    public string Provider { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Termii requires a pre-registered sender id; falls back to "BCKash" if unset.</summary>
    public string SenderId { get; set; } = "BCKash";

    /// <summary>
    /// Termii route: "generic" (default) or "dnd". Generic often doesn't reach Nigerian numbers on the
    /// Do-Not-Disturb register, which shows up as codes that never arrive; "dnd" does, but only once
    /// Termii has approved the sender id for that route — switch only after confirming with Termii.
    /// </summary>
    public string Channel { get; set; } = "generic";
}
