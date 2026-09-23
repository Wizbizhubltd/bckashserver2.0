namespace BCKash.Domain.Expenses;

/// <summary>
/// Pure "advance to next occurrence" logic for recurring expenses (FR-EXP-1). Same modeling as
/// <see cref="BCKash.Domain.Payroll.PayrollRecurrenceRules"/> — no scheduler exists in this
/// codebase, so this is called from an admin-triggered endpoint.
/// </summary>
public static class ExpenseRecurrenceRules
{
    public static DateOnly NextDate(DateOnly current, ExpenseRecurType type, string? recurFrequency)
    {
        var interval = ParseInterval(recurFrequency);
        return type switch
        {
            ExpenseRecurType.Day => current.AddDays(interval),
            ExpenseRecurType.Week => current.AddDays(interval * 7),
            ExpenseRecurType.Month => current.AddMonths(interval),
            ExpenseRecurType.Year => current.AddYears(interval),
            _ => current.AddMonths(interval),
        };
    }

    private static int ParseInterval(string? recurFrequency) =>
        int.TryParse(recurFrequency, out var parsed) && parsed > 0 ? parsed : 1;
}
