namespace BCKash.Application.PayrollProcessing;

/// <summary>
/// GL posting hook for a payroll run (BR-PAY-2, FR-PAY-2). Builds balanced posting lines via
/// <see cref="BCKash.Domain.GeneralLedger.PayrollGlPostingRules"/> and — unless the run's date
/// falls on/before an active GL closure for its office, or the run isn't fully configured for
/// GL — writes one <see cref="BCKash.Domain.GeneralLedger.GlJournalEntry"/> row per line.
/// </summary>
public interface IPayrollGlPostingService
{
    Task PostPayrollRunAsync(BCKash.Domain.Payroll.Payroll payroll, CancellationToken cancellationToken = default);
}
