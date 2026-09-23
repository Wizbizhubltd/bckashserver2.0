namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for a payroll run (BR-PAY-2, FR-PAY-2). Unlike
/// Loans/Assets/Expenses, the GL account roles aren't configured on a shared "product" —
/// <see cref="Payroll"/> itself carries <c>GlAccountExpenseId</c>/<c>GlAccountAssetId</c> per
/// run (the legacy schema has no payroll-template-level GL configuration). Debits the run's
/// expense account and credits its asset (cash) account for the net paid amount. Standard
/// double-entry, not extracted from or validated against the legacy PHP codebase (same caveat
/// as docs/gl-posting-spec.md). Returns an empty list — posting skipped, not partial — when the
/// run isn't fully configured for GL.
/// </summary>
public static class PayrollGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForPayrollRun(BCKash.Domain.Payroll.Payroll payroll, decimal netPay)
    {
        if (netPay <= 0 || payroll.GlAccountExpenseId is not int expense || payroll.GlAccountAssetId is not int asset)
        {
            return [];
        }

        return
        [
            new GlPostingLine(expense, Debit: netPay, Credit: null),
            new GlPostingLine(asset, Debit: null, Credit: netPay),
        ];
    }
}
