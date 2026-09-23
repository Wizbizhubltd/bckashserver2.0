namespace BCKash.Domain.Identity;

/// <summary>
/// New in this pass. Defaults to <see cref="Approved"/> so no pre-existing user is retroactively
/// locked out — only staff created through the new onboarding flow start <see cref="Pending"/>.
/// <see cref="Approved"/> is deliberately the zero/first value: EF Core treats a property's CLR
/// default as "unset" and applies the column's database-generated default in that case — if
/// Pending were the zero value, every genuinely-Pending insert would be silently promoted to
/// Approved instead (a real bug caught during development). Keeping them equal means "unset"
/// and "explicitly Approved" are the same thing, so there's no ambiguity to resolve.
/// </summary>
public enum UserOnboardingStatus
{
    Approved,
    Pending,
    Declined,
}
