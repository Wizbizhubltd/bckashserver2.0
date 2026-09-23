namespace BCKash.Domain.Identity;

/// <summary>
/// Pure maker-checker rules for staff onboarding. A super admin bypasses every rule here —
/// callers check <c>IsSuperAdmin</c> themselves before consulting these.
/// </summary>
public static class UserOnboardingRules
{
    /// <summary>Only a user_class Initiator may create a new staff record (as a Pending one awaiting authorization).</summary>
    public static bool CanInitiate(UserClass? actingUserClass) => actingUserClass == UserClass.Initiator;

    /// <summary>
    /// Only a user_class Authorizer of the *same user_type* as the record's initiator may
    /// approve/decline it — the core "same user_type of user_class authorizer" rule.
    /// </summary>
    public static bool CanAuthorize(UserClass? actingUserClass, string? actingUserType, string? initiatorUserType) =>
        actingUserClass == UserClass.Authorizer
        && actingUserType is not null
        && string.Equals(actingUserType, initiatorUserType, StringComparison.OrdinalIgnoreCase);

    public static bool CanTransition(UserOnboardingStatus current) => current == UserOnboardingStatus.Pending;
}
