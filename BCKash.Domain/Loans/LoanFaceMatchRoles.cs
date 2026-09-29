using BCKash.Domain.Identity;

namespace BCKash.Domain.Loans;

/// <summary>
/// Who may run the face match every client receiving a loan must pass before it can be disbursed —
/// the second face check, after the enrolment at onboarding. Super admins tick the user types in
/// Settings → Loan; it's stored as a comma-separated list under <see cref="SettingKey"/>. Until it's
/// saved, anyone with the loan-servicing permission may run it, as before.
/// </summary>
public static class LoanFaceMatchRoles
{
    public const string SettingKey = "loan_face_match_roles";

    /// <summary>The user types that can be ticked, most senior first.</summary>
    public static readonly string[] Eligible =
        [UserTypeSlugs.SuperAdmin, UserTypeSlugs.Director, UserTypeSlugs.Controller, UserTypeSlugs.Manager, UserTypeSlugs.Marketer];

    /// <summary>The ticked user types, or null when the setting has never been saved.</summary>
    public static IReadOnlySet<string>? Parse(string? value) =>
        value is null
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Eligible.Contains).ToHashSet();

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value)
    {
        if (key != SettingKey)
        {
            return null;
        }

        var slugs = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (slugs.Length == 0)
        {
            return "Tick at least one role — otherwise no loan could pass its face match and be disbursed.";
        }

        var unknown = slugs.Where(s => !Eligible.Contains(s)).ToList();
        return unknown.Count == 0 ? null : $"Unknown role(s): {string.Join(", ", unknown)}.";
    }
}
