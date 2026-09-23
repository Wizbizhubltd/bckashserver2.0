namespace BCKash.Domain.Organization;

/// <summary>Legacy values of `charges.product` (note: plural "shares", unlike `payment_type_details.type`'s singular "share").</summary>
public enum ChargeProduct
{
    Loan,
    Savings,
    Shares,
    Client,
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
