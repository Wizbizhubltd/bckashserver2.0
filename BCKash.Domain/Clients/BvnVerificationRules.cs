namespace BCKash.Domain.Clients;

/// <summary>
/// Settings → Customer Onboarding → "BVN verification mandatory" (<see cref="SettingKey"/>, "1"/"0").
/// On — the default, including while it has never been saved — every client onboarded must have their
/// BVN checked with the verification provider first. Off, the check is optional: a BVN is still entered
/// (11 digits, not already on the platform), staff can still verify it, but onboarding doesn't wait on
/// it — an unverified client can be verified later with "Verify now".
/// </summary>
public static class BvnVerificationRules
{
    public const string SettingKey = "bvn_verification_required";

    /// <summary>Only an explicit "0"/"false" turns it off.</summary>
    public static bool IsRequired(string? value) => value?.Trim().ToLowerInvariant() is not ("0" or "false");

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value) =>
        key == SettingKey && value?.Trim() is not ("0" or "1") ? "“BVN verification mandatory” must be switched on (1) or off (0)." : null;
}
