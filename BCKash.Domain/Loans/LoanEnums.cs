namespace BCKash.Domain.Loans;

/// <summary>`loans.client_type` / `loan_applications.client_type` — enum('client','group').</summary>
public enum LoanClientType
{
    Client,
    Group
}

/// <summary>
/// Shared days/weeks/months/years frequency enum — backs `loans.loan_term_type`,
/// `loans.repayment_frequency_type`, `loan_products.repayment_frequency_type` and
/// `loan_applications.loan_term_type` (all identical enum('days','weeks','months','years')).
/// </summary>
public enum FrequencyType
{
    Days,
    Weeks,
    Months,
    Years
}

/// <summary>
/// Shared day/week/month/year interest-rate frequency enum — backs `loans.interest_rate_type`
/// and `loan_products.interest_rate_type` (enum('day','week','month','year')).
/// </summary>
public enum InterestRateFrequencyType
{
    Day,
    Week,
    Month,
    Year
}

/// <summary>`loans.interest_method` / `loan_products.interest_method` — enum('flat','declining_balance').</summary>
public enum LoanInterestMethod
{
    Flat,
    DecliningBalance
}

/// <summary>
/// `loans.armotization_method` / `loan_products.armotization_method` (legacy column name misspelled;
/// C# type/property name uses the correct spelling) — enum('equal_installment','equal_principal').
/// </summary>
public enum LoanAmortizationMethod
{
    EqualInstallment,
    EqualPrincipal
}

/// <summary>`loans.status` — enum(13 values).</summary>
public enum LoanStatus
{
    New,
    Pending,
    Approved,
    NeedChanges,
    Disbursed,
    Declined,
    Rejected,
    Withdrawn,
    WrittenOff,
    Closed,
    PendingReschedule,
    Rescheduled,
    Paid
}

/// <summary>
/// Shared pending/approved/declined status — backs `loan_applications.status` and
/// `loan_transactions.status` (both enum('...pending','approved','declined...')).
/// </summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Declined
}

/// <summary>`loan_reschedule_requests.status` — enum('pending','approved','rejected').</summary>
public enum RescheduleRequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>`loan_products.interest_calculation_period_type` — enum('daily','same').</summary>
public enum InterestCalculationPeriodType
{
    Daily,
    Same
}

/// <summary>`loan_products.year_days` — enum('actual','360','364','365').</summary>
public enum YearDaysType
{
    Actual,
    Days360,
    Days364,
    Days365
}

/// <summary>`loan_products.month_days` — enum('actual','30','31').</summary>
public enum MonthDaysType
{
    Actual,
    Days30,
    Days31
}

/// <summary>`loan_products.loan_transaction_strategy` — enum(3 values).</summary>
public enum LoanTransactionStrategy
{
    PenaltyFeesInterestPrincipal,
    PrincipalInterestPenaltyFees,
    InterestPrincipalPenaltyFees
}

/// <summary>`loan_products.accounting_rule` — enum('none','cash','accrual_periodic','accrual_upfront').</summary>
public enum LoanAccountingRule
{
    None,
    Cash,
    AccrualPeriodic,
    AccrualUpfront
}

/// <summary>`loan_charges.charge_type` — enum(7 values).</summary>
public enum LoanChargeType
{
    Disbursement,
    DisbursementRepayment,
    SpecifiedDueDate,
    InstallmentFee,
    OverdueInstallmentFee,
    LoanReschedulingFee,
    OverdueMaturity
}

/// <summary>`loan_charges.charge_option` — enum(8 values), how the charge amount is calculated.</summary>
public enum LoanChargeCalculationType
{
    Flat,
    Percentage,
    InstallmentPrincipalDue,
    InstallmentPrincipalInterestDue,
    InstallmentInterestDue,
    InstallmentTotalDue,
    TotalDue,
    OriginalPrincipal
}

/// <summary>`loan_transactions.transaction_type` — enum(25 values); see FR-LN-17.</summary>
public enum LoanTransactionType
{
    Repayment,
    RepaymentDisbursement,
    WriteOff,
    WriteOffRecovery,
    Disbursement,
    InterestAccrual,
    FeeAccrual,
    PenaltyAccrual,
    Deposit,
    Withdrawal,
    ManualEntry,
    PayCharge,
    TransferFund,
    Interest,
    Income,
    Fee,
    DisbursementFee,
    InstallmentFee,
    SpecifiedDueDateFee,
    OverdueMaturity,
    OverdueInstallmentFee,
    LoanReschedulingFee,
    Penalty,
    InterestWaiver,
    ChargeWaiver
}

/// <summary>`loan_transactions.reversal_type` — enum('system','user','none').</summary>
public enum LoanTransactionReversalType
{
    System,
    User,
    None
}

/// <summary>`loan_transactions.payment_apply_to` — enum('interest','principal','fees','penalty','regular').</summary>
public enum LoanPaymentApplyTo
{
    Interest,
    Principal,
    Fees,
    Penalty,
    Regular
}
