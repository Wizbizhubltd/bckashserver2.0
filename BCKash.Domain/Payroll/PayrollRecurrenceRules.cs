namespace BCKash.Domain.Payroll;

/// <summary>
/// Pure "advance to next occurrence" logic for recurring payroll (FR-PAY-3). No scheduler
/// exists anywhere in this codebase (same situation as FR-SAV-4/FR-LN-25) — this is called from
/// an admin-triggered endpoint, not a background job. `RecurFrequency` is a legacy free-text
/// column; it's read here as the interval count (e.g. "2" + Weeks = every 2 weeks), defaulting
/// to 1 when it isn't a parseable positive integer.
/// </summary>
public static class PayrollRecurrenceRules
{
    public static DateOnly NextDate(DateOnly current, PayrollRecurType type, string? recurFrequency)
    {
        var interval = ParseInterval(recurFrequency);
        return type switch
        {
            PayrollRecurType.Days => current.AddDays(interval),
            PayrollRecurType.Weeks => current.AddDays(interval * 7),
            PayrollRecurType.Months => current.AddMonths(interval),
            PayrollRecurType.Years => current.AddYears(interval),
            _ => current.AddMonths(interval),
        };
    }

    private static int ParseInterval(string? recurFrequency) =>
        int.TryParse(recurFrequency, out var parsed) && parsed > 0 ? parsed : 1;
}
