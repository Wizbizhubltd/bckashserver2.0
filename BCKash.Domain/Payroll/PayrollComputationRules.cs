namespace BCKash.Domain.Payroll;

/// <summary>One template line item snapshotted at run time, as stored on a <see cref="PayrollMeta"/> row.</summary>
public record PayrollLineInput(int? PayrollTemplateMetaId, PayrollTemplateMetaType Type, bool IsTax, bool IsPercentage, PayrollTemplateMetaTaxOn? TaxOn, decimal Value);

/// <summary>The computed amount for one line, signed (positive for additions, negative for deductions/taxes).</summary>
public record PayrollLineResult(int? PayrollTemplateMetaId, decimal Amount);

public record PayrollComputationResult(decimal NetPay, IReadOnlyList<PayrollLineResult> Lines);

/// <summary>
/// Pure net-pay computation from a payroll template's line items (FR-PAY-1, FR-PAY-2). No
/// legacy PHP source was available to confirm the exact computation order for mixed
/// fixed/percentage/tax lines, so this documents the modeling choice made here (same
/// "no legacy source, documented assumption" situation as
/// docs/interest-calculation-spec.md's FR-LN-15 caveat) — see docs/payroll-computation-spec.md:
///
/// 1. Non-tax lines (IsTax = false) are applied first, each against the gross amount:
///    fixed lines add/subtract their Value outright; percentage lines add/subtract
///    Value% of the gross amount. Their running total produces an intermediate net.
/// 2. Tax lines (IsTax = true) are applied second, so a tax whose TaxOn is Net can be based on
///    the intermediate net produced by step 1, or Gross for the original gross amount.
/// 3. Every line's Type (Addition/Deduction) determines its sign, tax or not — IsTax only
///    controls *what base a percentage is computed against*, not the sign.
/// </summary>
public static class PayrollComputationRules
{
    public static PayrollComputationResult Compute(decimal gross, IReadOnlyList<PayrollLineInput> lines)
    {
        var results = new List<PayrollLineResult>();
        var runningNet = gross;

        foreach (var line in lines.Where(l => !l.IsTax))
        {
            var amount = line.IsPercentage ? Math.Round(gross * line.Value / 100m, 2) : line.Value;
            var signed = line.Type == PayrollTemplateMetaType.Deduction ? -amount : amount;
            runningNet += signed;
            results.Add(new PayrollLineResult(line.PayrollTemplateMetaId, signed));
        }

        foreach (var line in lines.Where(l => l.IsTax))
        {
            var basis = line.TaxOn == PayrollTemplateMetaTaxOn.Gross ? gross : runningNet;
            var amount = line.IsPercentage ? Math.Round(basis * line.Value / 100m, 2) : line.Value;
            var signed = line.Type == PayrollTemplateMetaType.Deduction ? -amount : amount;
            runningNet += signed;
            results.Add(new PayrollLineResult(line.PayrollTemplateMetaId, signed));
        }

        return new PayrollComputationResult(runningNet, results);
    }
}
