namespace BCKash.Domain.Clients;

/// <summary>
/// Settings → Loan → "Face capture mandatory" (<see cref="SettingKey"/>, "1"/"0"). On — the default,
/// including while it has never been saved — a client needs an enrolled face to be approved and to
/// take a loan, and everyone receiving a loan must pass a face match before it's disbursed. Off, face
/// capture is optional everywhere: staff can still capture and match faces, but nothing waits on it.
/// </summary>
public static class FaceCaptureRules
{
    public const string SettingKey = "face_capture_required";

    /// <summary>Only an explicit "0"/"false" turns it off.</summary>
    public static bool IsRequired(string? value) => value?.Trim().ToLowerInvariant() is not ("0" or "false");

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value) =>
        key == SettingKey && value?.Trim() is not ("0" or "1") ? "“Face capture mandatory” must be switched on (1) or off (0)." : null;
}
