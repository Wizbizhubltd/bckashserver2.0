namespace BCKash.Application.PayrollProcessing;

public enum PayrollRunOutcome
{
    Success,
    TemplateNotFound,

    /// <summary>Gross amount must be positive.</summary>
    InvalidAmount,
}

public record PayrollRunResult(PayrollRunOutcome Outcome, BCKash.Domain.Payroll.Payroll? Payroll = null);

/// <summary>
/// Runs payroll for one employee/user for a period (BR-PAY-2, FR-PAY-2): snapshots the
/// template's line items onto <see cref="PayrollMeta"/> rows, computes net pay via
/// <see cref="PayrollComputationRules"/>, and posts GL via <see cref="IPayrollGlPostingService"/>.
/// </summary>
public interface IPayrollService
{
    /// <summary>
    /// <paramref name="payroll"/> must have GrossAmount and (optionally) PayrollTemplateId set;
    /// PaidAmount is computed here and any caller-supplied value is overwritten.
    /// </summary>
    Task<PayrollRunResult> RunAsync(BCKash.Domain.Payroll.Payroll payroll, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances every recurring payroll run whose RecurNextDate is due (&lt;= today), cloning a
    /// new run dated on the due date (recomputed against the template's *current* line items)
    /// and moving the original's RecurNextDate forward (FR-PAY-3). No scheduler exists in this
    /// codebase, so this is admin-triggered.
    /// </summary>
    Task<int> GenerateDueRecurringAsync(CancellationToken cancellationToken = default);
}
