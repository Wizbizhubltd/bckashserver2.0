namespace BCKash.Application.Loans;

/// <summary>
/// Settings → Loan → "Overdue &amp; penalty rules": when an instalment or a loan officially counts as
/// overdue, and whether penalties are charged automatically.
/// </summary>
public record OverdueRules(int RepaymentOverdueDays, int LoanOverdueDays, bool AutoApplyPenalty)
{
    public static readonly OverdueRules Default = new(0, 0, false);
}

public static class OverdueRuleKeys
{
    /// <summary>Days after an instalment's due date before it counts as overdue.</summary>
    public const string RepaymentOverdueDays = "auto_overdue_repayment_days";

    /// <summary>Days after a loan's final repayment date before it counts as defaulted.</summary>
    public const string LoanOverdueDays = "auto_overdue_loan_days";

    public const string AutoApplyPenalty = "auto_apply_penalty";

    public const int MaxDays = 365;

    public static readonly string[] All = [RepaymentOverdueDays, LoanOverdueDays, AutoApplyPenalty];

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return key switch
        {
            RepaymentOverdueDays or LoanOverdueDays when !int.TryParse(trimmed, out var days) || days < 0 || days > MaxDays
                => $"Enter a whole number of days from 0 to {MaxDays}.",
            AutoApplyPenalty when trimmed is not ("0" or "1")
                => "Automatic penalties must be switched on (1) or off (0).",
            _ => null,
        };
    }
}

public interface IOverdueRulesProvider
{
    /// <summary>Not cached beyond the query cache — a saved change applies to the next read.</summary>
    Task<OverdueRules> GetAsync(CancellationToken cancellationToken = default);
}

public record PenaltyRunResult(int LoansCharged, int PenaltiesCharged, decimal TotalCharged, bool Skipped);

/// <summary>Charges due late-repayment and default penalties (Settings → Fees &amp; Payments) onto overdue loans.</summary>
public interface ILoanPenaltyService
{
    /// <summary>Idempotent: safe to run any number of times a day. Does nothing while automatic penalties are off.</summary>
    Task<PenaltyRunResult> RunDueAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default);
}
