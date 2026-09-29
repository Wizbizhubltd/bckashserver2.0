namespace BCKash.Application.Communications;

/// <summary>
/// Settings → Notifications → "SMS sending": the master switch for every SMS the system sends —
/// sign-in and confirmation codes as well as campaign messages. Emails are unaffected.
/// </summary>
public interface ISmsSwitch
{
    /// <summary>The legacy `settings` key; stored "1"/"0" (older rows may say "true"/"false").</summary>
    public const string SettingKey = "sms_enabled";

    /// <summary>True unless a super admin has switched SMS off — a never-saved setting means on.</summary>
    Task<bool> IsOnAsync(CancellationToken cancellationToken = default);
}
