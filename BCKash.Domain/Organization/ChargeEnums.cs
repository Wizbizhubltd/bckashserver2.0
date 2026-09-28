namespace BCKash.Domain.Organization;

/// <summary>Legacy values of `charges.product` (note: plural "shares", unlike `payment_type_details.type`'s singular "share").</summary>
public enum ChargeProduct
{
    Loan,
    Savings,
    Shares,
    Client,

    /// <summary>New (not in legacy data): fees charged to a lending group, e.g. registration or monthly dues.</summary>
    Group,
}

/// <summary>Legacy values of `charges.charge_type`.</summary>
public enum ChargeType
{
    Disbursement,
    DisbursementRepayment,
    SpecifiedDueDate,
    InstallmentFee,
    OverdueInstallmentFee,
    LoanReschedulingFee,
    OverdueMaturity,
    SavingsActivation,
    WithdrawalFee,
    AnnualFee,
    MonthlyFee,
    Activation,
    SharesPurchase,
    SharesRedeem,

    /// <summary>New (not in legacy data): a penalty for paying a loan off before its schedule ends (early closure / prepayment).</summary>
    EarlyRepayment,
}

/// <summary>Legacy values of `charges.charge_option`.</summary>
public enum ChargeOption
{
    Flat,
    Percentage,
    InstallmentPrincipalDue,
    InstallmentPrincipalInterestDue,
    InstallmentInterestDue,
    InstallmentTotalDue,
    TotalDue,
    PrincipalDue,
    InterestDue,
    TotalOutstanding,
    OriginalPrincipal,
}

/// <summary>Legacy values of `charges.charge_frequency_type`.</summary>
public enum ChargeFrequencyType
{
    Days,
    Weeks,
    Months,
    Years,
}

/// <summary>Legacy values of `charges.charge_payment_mode`.</summary>
public enum ChargePaymentMode
{
    Regular,
    AccountTransfer,
}
