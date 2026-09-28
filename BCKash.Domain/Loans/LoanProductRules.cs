namespace BCKash.Domain.Loans;

/// <summary>
/// What a loan product must look like to be saved, beyond <see cref="LoanProductValidationRules"/>'
/// min ≤ default ≤ max: a name, a positive maximum amount, positive terms, a real repayment interval, interest
/// within 0–100% per period, and grace periods that fit inside the loan.
/// </summary>
public static class LoanProductRules
{
    public const decimal MaxInterestRate = 100;

    /// <summary>Null when valid; otherwise what's wrong, in plain words.</summary>
    public static string? Validate(LoanProduct p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) return "Give the product a name.";
        if (p.Name.Trim().Length > 191) return "The product name must be 191 characters or fewer.";

        // A minimum of 0 is the legacy way of saying "no minimum" (most imported products use it).
        if (Negative(p.MinimumPrincipal, p.DefaultPrincipal, p.MaximumPrincipal)) return "Loan amounts can't be negative.";
        if (p.MaximumPrincipal is <= 0) return "The largest loan amount must be more than zero.";
        if (NegativeOrZero(p.MinimumLoanTerm, p.DefaultLoanTerm, p.MaximumLoanTerm)) return "Loan terms must be at least 1.";
        if (p.RepaymentFrequency is < 1) return "Repayments must be at least 1 period apart.";
        if (Outside(p.MinimumInterestRate) || Outside(p.DefaultInterestRate) || Outside(p.MaximumInterestRate))
            return $"Interest rates must be between 0% and {MaxInterestRate}%.";

        if (!LoanProductValidationRules.IsValidMinDefaultMax(p.MinimumPrincipal, p.DefaultPrincipal, p.MaximumPrincipal)
            || !LoanProductValidationRules.IsValidMinDefaultMax(p.MinimumLoanTerm, p.DefaultLoanTerm, p.MaximumLoanTerm)
            || !LoanProductValidationRules.IsValidMinDefaultMax(p.MinimumInterestRate, p.DefaultInterestRate, p.MaximumInterestRate))
            return "Minimum must be ≤ default, and default must be ≤ maximum, for amount, term and interest rate.";

        if (p.GraceOnPrincipal is < 0 || p.GraceOnInterestCharged is < 0 || p.GraceOnInterestPayment is < 0) return "Grace periods can't be negative.";
        if (p.GraceOnPrincipal is { } principalGrace && p.MaximumLoanTerm is { } maxTerm && principalGrace >= maxTerm)
            return "The grace period on principal must be shorter than the longest loan term.";
        if (p.NpaDays is < 0 || p.ArrearsGraceDays is < 0) return "Arrears and non-performing days can't be negative.";

        return null;
    }

    private static bool Negative(params decimal?[] values) => values.Any(v => v is < 0);

    private static bool NegativeOrZero(params int?[] values) => values.Any(v => v is < 1);

    private static bool Outside(decimal? rate) => rate is < 0 or > MaxInterestRate;
}
