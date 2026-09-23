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
}
