namespace BCKash.Domain.Reporting;

/// <summary>
/// Pure "advance to next occurrence" logic for scheduled reports (FR-RPT-2). Same modeling as
/// <see cref="BCKash.Domain.Communications.CampaignRecurrenceRules"/>.
/// </summary>
public static class ReportSchedulerRecurrenceRules
{
    public static DateOnly NextDate(DateOnly current, ReportSchedulerRecurFrequency frequency, string? recurInterval)
    {
        var interval = ParseInterval(recurInterval);
        return frequency switch
        {
            ReportSchedulerRecurFrequency.Daily => current.AddDays(interval),
            ReportSchedulerRecurFrequency.Weekly => current.AddDays(interval * 7),
            ReportSchedulerRecurFrequency.Monthly => current.AddMonths(interval),
            ReportSchedulerRecurFrequency.Yearly => current.AddYears(interval),
            _ => current.AddMonths(interval),
        };
    }

    private static int ParseInterval(string? recurInterval) =>
        int.TryParse(recurInterval, out var parsed) && parsed > 0 ? parsed : 1;
}
