namespace BCKash.Application.Auth;

/// <summary>Custom claims carried by BCKash access tokens, beyond the standard sub/email ones.</summary>
public static class AuthClaimTypes
{
    public const string Permission = "permission";
    public const string OfficeId = "office_id";

    /// <summary>The user's user_type slug (see <see cref="Domain.Identity.UserTypeSlugs"/>).</summary>
    public const string UserType = "user_type";

    /// <summary>The sign-in session the token belongs to; only the user's current session is accepted.</summary>
    public const string SessionId = "sid";

    /// <summary>Present (value "true") while the user must change their temporary password before doing anything else.</summary>
    public const string PasswordChangeRequired = "pwd_change_required";
}
