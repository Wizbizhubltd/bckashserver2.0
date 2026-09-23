namespace BCKash.Domain.Identity;

/// <summary>
/// New in this pass — not part of the legacy schema. A maker-checker classification layered on
/// top of a user's <c>user_type</c> (role): an Initiator can start an operation, an Authorizer
/// of the *same* user_type can approve/decline it, and a Reviewer can observe but do neither.
/// Super admins are the sole exception — they bypass this restriction entirely (see
/// docs/staff-onboarding-rbac-spec.md).
/// </summary>
public enum UserClass
{
    Initiator,
    Authorizer,
    Reviewer,
}
