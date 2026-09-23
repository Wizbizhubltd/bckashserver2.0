namespace BCKash.Domain.Organization;

/// <summary>
/// Which <see cref="ChargeType"/>/<see cref="ChargeOption"/> combinations are legal for a
/// given <see cref="ChargeProduct"/>. The legacy schema has no constraint enforcing this
/// (charge_type/charge_option/product are three independent enum columns) — this matrix is
/// inferred from the enum values' own names (e.g. "savings_activation" only makes sense for
/// Product = Savings; the installment/principal/interest-due options only make sense for a
/// loan's repayment schedule). Best-effort per the BRD's "undocumented business rules" risk
/// item — flagged [VALIDATE WITH BUSINESS] in FRD.md alongside the other inferred rules.
/// </summary>
public static class ChargeValidationRules
{
    private static readonly Dictionary<ChargeType, ChargeProduct> ChargeTypeProduct = new()
    {
        [ChargeType.Disbursement] = ChargeProduct.Loan,
        [ChargeType.DisbursementRepayment] = ChargeProduct.Loan,
        [ChargeType.SpecifiedDueDate] = ChargeProduct.Loan,
        [ChargeType.InstallmentFee] = ChargeProduct.Loan,
        [ChargeType.OverdueInstallmentFee] = ChargeProduct.Loan,
        [ChargeType.LoanReschedulingFee] = ChargeProduct.Loan,
        [ChargeType.OverdueMaturity] = ChargeProduct.Loan,
        [ChargeType.SavingsActivation] = ChargeProduct.Savings,
        [ChargeType.WithdrawalFee] = ChargeProduct.Savings,
        [ChargeType.AnnualFee] = ChargeProduct.Savings,
        [ChargeType.MonthlyFee] = ChargeProduct.Savings,
        [ChargeType.SharesPurchase] = ChargeProduct.Shares,
        [ChargeType.SharesRedeem] = ChargeProduct.Shares,
        // "activation" alone (as opposed to "savings_activation") reads as the generic
        // client-onboarding charge — the one ChargeType without a product-specific prefix.
        [ChargeType.Activation] = ChargeProduct.Client,
    };

    // These ChargeOption values only make sense against a loan's repayment schedule
    // (principal/interest/total *due*, outstanding balance, original principal) — every
    // other product may only use a flat amount or a percentage.
    private static readonly HashSet<ChargeOption> LoanOnlyOptions =
    [
        ChargeOption.InstallmentPrincipalDue,
        ChargeOption.InstallmentPrincipalInterestDue,
        ChargeOption.InstallmentInterestDue,
        ChargeOption.InstallmentTotalDue,
        ChargeOption.TotalDue,
        ChargeOption.PrincipalDue,
        ChargeOption.InterestDue,
        ChargeOption.TotalOutstanding,
        ChargeOption.OriginalPrincipal,
    ];

    public static bool IsValidChargeTypeForProduct(ChargeType chargeType, ChargeProduct product) =>
        ChargeTypeProduct.TryGetValue(chargeType, out var requiredProduct) && requiredProduct == product;

    public static bool IsValidChargeOptionForProduct(ChargeOption chargeOption, ChargeProduct product) =>
        product == ChargeProduct.Loan || !LoanOnlyOptions.Contains(chargeOption);
}
