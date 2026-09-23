namespace BCKash.Domain.Loans;

/// <summary>
/// Loan-level transitions. FR-LN-7's need-changes/pending cycle is reachable only in the window
/// between an application's approval (which creates the Loan in Pending status) and disbursement.
/// FR-LN-8's disbursement transition moves Pending to Disbursed (a loan in NeedChanges must be
/// resubmitted first, per FR-LN-7's own workflow) and now generates the repayment schedule — see
/// docs/interest-calculation-spec.md for the best-effort, unvalidated-against-legacy formula this
/// uses (FR-LN-15 remains formally unresolved). FR-LN-24's write-off is reachable only from
/// Disbursed. Reschedule (FR-LN-23) moves a Disbursed loan to Rescheduled via a separate
/// approve/reject workflow on LoanRescheduleRequest, not a direct status-only transition here.
/// </summary>
public static class LoanTransitionRules
{
    public static bool CanRequestChanges(LoanStatus current) => current == LoanStatus.Pending;

    public static bool CanResubmit(LoanStatus current) => current == LoanStatus.NeedChanges;

    public static bool CanDisburse(LoanStatus current) => current == LoanStatus.Pending;

    public static bool CanWriteOff(LoanStatus current) => current is LoanStatus.Disbursed or LoanStatus.Rescheduled;

    /// <summary>Whether a loan can currently receive a repayment/waiver, or be rescheduled/reversed against — Disbursed, or Rescheduled (a schedule still exists and is still owed).</summary>
    public static bool HasActiveSchedule(LoanStatus current) => current is LoanStatus.Disbursed or LoanStatus.Rescheduled;
}
