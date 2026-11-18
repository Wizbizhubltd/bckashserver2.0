namespace BCKash.Domain.Identity;

/// <summary>
/// Settings → Roles &amp; Access → "Lock accounts after failed sign-ins" (<see cref="SettingKey"/>, "1"/"0").
/// Off — the default, including while it has never been saved — no account or IP is ever locked out:
/// staff were being stopped with "Too many failed attempts" while learning to sign in with their
/// temporary passwords. On, the LoginThrottle limits apply (wrong passwords per account, failures per
/// IP). Failed attempts are recorded either way, so switching it on takes effect straight away.
/// </summary>
public static class LoginLockoutRules
{
    public const string SettingKey = "login_lockout_enabled";

    /// <summary>Only an explicit "1"/"true" turns it on.</summary>
    public static bool IsEnabled(string? value) => value?.Trim().ToLowerInvariant() is "1" or "true";

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value) =>
        key == SettingKey && value?.Trim() is not ("0" or "1") ? "“Lock accounts after failed sign-ins” must be switched on (1) or off (0)." : null;
}
