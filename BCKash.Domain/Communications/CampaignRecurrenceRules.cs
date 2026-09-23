namespace BCKash.Domain.Communications;

/// <summary>
/// Pure "advance to next occurrence" logic for recurring campaigns (FR-COM-2). Same modeling as
/// <see cref="BCKash.Domain.Payroll.PayrollRecurrenceRules"/>/<see cref="BCKash.Domain.Expenses.ExpenseRecurrenceRules"/> —
/// `RecurInterval` is read as the interval count, defaulting to 1 when it isn't a parseable
/// positive integer.
/// </summary>
public static class CampaignRecurrenceRules
{
    public static DateOnly NextDate(DateOnly current, CampaignRecurFrequency frequency, string? recurInterval)
    {
        var interval = ParseInterval(recurInterval);
        return frequency switch
        {
            CampaignRecurFrequency.Days => current.AddDays(interval),
            CampaignRecurFrequency.Weeks => current.AddDays(interval * 7),
            CampaignRecurFrequency.Months => current.AddMonths(interval),
            CampaignRecurFrequency.Years => current.AddYears(interval),
            _ => current.AddMonths(interval),
        };
    }

    private static int ParseInterval(string? recurInterval) =>
        int.TryParse(recurInterval, out var parsed) && parsed > 0 ? parsed : 1;
}
