namespace BCKash.Infrastructure.Identity;

/// <summary>
/// Where staff sign in (the office portal), linked from staff emails such as a forced password
/// reset or an office transfer. Bound from the "StaffPortal" section — `StaffPortal__Url` in .env.
/// </summary>
public class StaffPortalSettings
{
    public const string SectionName = "StaffPortal";

    public string? Url { get; set; }

    /// <summary>
    /// The password a bulk forced reset gives every staff member (`StaffPortal__DefaultResetPassword`).
    /// Must be exactly <see cref="DefaultResetPasswordLength"/> characters; when blank or the wrong
    /// length, each staff member gets their own random one instead. Either way they must change it
    /// when they next sign in.
    /// </summary>
    public string? DefaultResetPassword { get; set; }

    public const int DefaultResetPasswordLength = 8;
}
