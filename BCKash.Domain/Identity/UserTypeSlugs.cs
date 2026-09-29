namespace BCKash.Domain.Identity;

/// <summary>
/// "user_type" is deliberately built on top of the existing legacy `roles`/`role_users` tables
/// rather than a new column — a user's type IS the one Role assigned to them whose slug is one
/// of these five, so RBAC continues to flow through the pre-existing
/// Permission → RolePermission → RoleUser chain with no schema change and no data-migration
/// risk. See docs/staff-onboarding-rbac-spec.md.
/// </summary>
public static class UserTypeSlugs
{
    public const string SuperAdmin = "super_admin";
    public const string Controller = "controller";
    public const string Director = "director";
    public const string Manager = "manager";
    public const string Marketer = "marketer";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Controller, Director, Manager, Marketer];

    /// <summary>
    /// Seniority, highest first: super admin → director (several zones) → controller (one office,
    /// above its manager) → manager → marketer. Staff below a super admin may only manage staff
    /// ranked strictly below themselves. 0 for no/unknown type.
    /// </summary>
    public static int Rank(string? slug) => slug switch
    {
        SuperAdmin => 5,
        Director => 4,
        Controller => 3,
        Manager => 2,
        Marketer => 1,
        _ => 0,
    };

    public static bool Outranks(string? actingSlug, string? targetSlug) => Rank(actingSlug) > Rank(targetSlug);

    /// <summary>
    /// Only managers and marketers register new clients; directors, controllers and super admins
    /// oversee the client book. A user with no user_type keeps the access they had before this rule.
    /// </summary>
    public static bool CanCreateClients(string? slug) => slug is null or Manager or Marketer;

    /// <summary>Controllers approve (or decline) the clients managers and marketers onboard; super admins can too, from the control portal. A user with no user_type keeps their old access.</summary>
    public static bool CanApproveClients(string? slug) => slug is null or Controller or SuperAdmin;

    /// <summary>Who grants or refuses a request to edit an approved client: controllers, and super admins from the control portal.</summary>
    public static bool CanReviewEditRequests(string? slug) => slug is Controller or SuperAdmin;
}
