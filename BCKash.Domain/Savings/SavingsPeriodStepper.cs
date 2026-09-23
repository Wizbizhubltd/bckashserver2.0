namespace BCKash.Domain.Savings;

/// <summary>Advances a date by one compounding/posting period — shared by account-approval setup and the interest-posting engine (FR-SAV-4).</summary>
public static class SavingsPeriodStepper
{
    public static DateOnly AddCompoundingPeriod(DateOnly date, InterestCompoundingPeriod? period) => period switch
    {
        InterestCompoundingPeriod.Daily => date.AddDays(1),
        InterestCompoundingPeriod.Quarterly => date.AddMonths(3),
        InterestCompoundingPeriod.Biannual => date.AddMonths(6),
        InterestCompoundingPeriod.Annually => date.AddYears(1),
        _ => date.AddMonths(1), // Monthly, or unset — the most common default.
    };

    public static DateOnly AddPostingPeriod(DateOnly date, InterestPostingPeriod? period) => period switch
    {
        InterestPostingPeriod.Quarterly => date.AddMonths(3),
        InterestPostingPeriod.Biannual => date.AddMonths(6),
        InterestPostingPeriod.Annually => date.AddYears(1),
        _ => date.AddMonths(1), // Monthly, or unset — the most common default.
    };
}
