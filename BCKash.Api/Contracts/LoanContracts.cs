using BCKash.Domain.Identity;
using BCKash.Domain.Loans;

namespace BCKash.Api.Contracts;

// ---- Loan Products ----

public record LoanProductResponse(
    int Id, string? Name, string? ShortName, string? Description, int? FundId, int? CurrencyId, int Decimals,
    decimal? MinimumPrincipal, decimal? DefaultPrincipal, decimal? MaximumPrincipal,
    int? MinimumLoanTerm, int? DefaultLoanTerm, int? MaximumLoanTerm,
    int? RepaymentFrequency, FrequencyType? RepaymentFrequencyType,
    decimal? MinimumInterestRate, decimal? DefaultInterestRate, decimal? MaximumInterestRate, InterestRateFrequencyType? InterestRateType,
    int? GraceOnInterestCharged, int? GraceOnPrincipal, int? GraceOnInterestPayment, bool AllowCustomGrace, bool AllowStandingInstructions,
    LoanInterestMethod? InterestMethod, LoanAmortizationMethod? AmortizationMethod,
    InterestCalculationPeriodType InterestCalculationPeriodType, YearDaysType YearDays, MonthDaysType MonthDays,
    LoanTransactionStrategy LoanTransactionStrategy, bool IncludeInCycle, bool LockGuarantee, bool AllocateOverpayments, bool AllowAdditionalCharges,
    LoanAccountingRule AccountingRule, int? NpaDays, int? ArrearsGraceDays, bool NpaSuspendIncome,
    int? GlAccountFundSourceId, int? GlAccountLoanPortfolioId, int? GlAccountReceivableInterestId, int? GlAccountReceivableFeeId,
    int? GlAccountReceivablePenaltyId, int? GlAccountLoanOverPaymentsId, int? GlAccountSuspendedIncomeId, int? GlAccountIncomeInterestId,
    int? GlAccountIncomeFeeId, int? GlAccountIncomePenaltyId, int? GlAccountIncomeRecoveryId, int? GlAccountLoansWrittenOffId,
    bool Active);

public record SaveLoanProductRequest(
    string? Name, string? ShortName, string? Description, int? FundId, int? CurrencyId, int Decimals,
    decimal? MinimumPrincipal, decimal? DefaultPrincipal, decimal? MaximumPrincipal,
    int? MinimumLoanTerm, int? DefaultLoanTerm, int? MaximumLoanTerm,
    int? RepaymentFrequency, FrequencyType? RepaymentFrequencyType,
    decimal? MinimumInterestRate, decimal? DefaultInterestRate, decimal? MaximumInterestRate, InterestRateFrequencyType? InterestRateType,
    int? GraceOnInterestCharged, int? GraceOnPrincipal, int? GraceOnInterestPayment, bool AllowCustomGrace, bool AllowStandingInstructions,
    LoanInterestMethod? InterestMethod, LoanAmortizationMethod? AmortizationMethod,
    InterestCalculationPeriodType InterestCalculationPeriodType, YearDaysType YearDays, MonthDaysType MonthDays,
    LoanTransactionStrategy LoanTransactionStrategy, bool IncludeInCycle, bool LockGuarantee, bool AllocateOverpayments, bool AllowAdditionalCharges,
    LoanAccountingRule AccountingRule, int? NpaDays, int? ArrearsGraceDays, bool NpaSuspendIncome,
    int? GlAccountFundSourceId, int? GlAccountLoanPortfolioId, int? GlAccountReceivableInterestId, int? GlAccountReceivableFeeId,
    int? GlAccountReceivablePenaltyId, int? GlAccountLoanOverPaymentsId, int? GlAccountSuspendedIncomeId, int? GlAccountIncomeInterestId,
    int? GlAccountIncomeFeeId, int? GlAccountIncomePenaltyId, int? GlAccountIncomeRecoveryId, int? GlAccountLoansWrittenOffId);

// ---- Loan Purposes / Collateral Types (simple lookups) ----

public record LoanPurposeResponse(int Id, string? Name);

public record SaveLoanPurposeRequest(string? Name);

public record CollateralTypeResponse(int Id, string? Name);

public record SaveCollateralTypeRequest(string? Name);

// ---- Loan Applications ----

public record LoanApplicationListItemResponse(
    int Id, LoanClientType ClientType, int? ClientId, int? GroupId, int? OfficeId, int LoanProductId,
    decimal Amount, ApprovalStatus Status, int? LoanId);

public record LoanApplicationResponse(
    int Id, LoanClientType ClientType, int? UserId, int? LoanId, int? LoanPurposeId, int? CurrencyId,
    int? OfficeId, int? ClientId, int? GroupId, int LoanProductId, decimal Amount, ApprovalStatus Status,
    int? LoanTerm, FrequencyType? LoanTermType, int? ApprovedById, int? DeclinedById,
    string? ApprovedNotes, string? DeclinedNotes, DateOnly? DeclinedDate, DateOnly? ApprovedDate, string? Notes);

public record CreateLoanApplicationRequest(
    LoanClientType ClientType, int? LoanPurposeId, int? CurrencyId, int? OfficeId, int? ClientId, int? GroupId,
    int LoanProductId, decimal Amount, int? LoanTerm, FrequencyType? LoanTermType, string? Notes);

public record UpdateLoanApplicationRequest(
    LoanClientType ClientType, int? LoanPurposeId, int? CurrencyId, int? OfficeId, int? ClientId, int? GroupId,
    int LoanProductId, decimal Amount, int? LoanTerm, FrequencyType? LoanTermType, string? Notes);

public record ApproveLoanApplicationRequest(decimal ApprovedAmount, string? Notes);

// ---- Guarantors / Collateral ----

public record GuarantorResponse(
    int Id, int? ClientId, int? LoanId, int? LoanApplicationId, bool IsClient, int? ClientRelationshipId, decimal? Amount,
    string? Title, string? FirstName, string? MiddleName, string? LastName, Gender? Gender, DateOnly? Dob,
    string? Street, string? Address, string? Mobile, string? Phone, string? Email, string? Work, string? WorkAddress, bool LockFunds);

public record SaveGuarantorRequest(
    int? ClientId, bool IsClient, int? ClientRelationshipId, decimal? Amount,
    string? Title, string? FirstName, string? MiddleName, string? LastName, Gender? Gender, DateOnly? Dob,
    string? Street, string? Address, string? Mobile, string? Phone, string? Email, string? Work, string? WorkAddress, bool LockFunds);

public record CollateralResponse(
    int Id, int? LoanId, int? LoanApplicationId, int? ClientId, int? CollateralTypeId,
    string? Name, string? Serial, decimal? Value, string? Description);

public record SaveCollateralRequest(int? ClientId, int? CollateralTypeId, string? Name, string? Serial, decimal? Value, string? Description);

// ---- Loans (read-only in Phase 4, plus request-changes/resubmit) ----

public record LoanListItemResponse(
    int Id, string? AccountNumber, int? ClientId, int? GroupId, int? OfficeId, int? LoanProductId,
    decimal? AppliedAmount, decimal? ApprovedAmount, LoanStatus Status);

public record LoanResponse(
    int Id, LoanClientType ClientType, int? LoanProductId, int? ClientId, int? OfficeId, int? GroupId,
    int? LoanPurposeId, int? CurrencyId, string? AccountNumber, decimal? Principal, decimal? AppliedAmount, decimal? ApprovedAmount,
    int? LoanTerm, FrequencyType? LoanTermType, decimal? InterestRate, InterestRateFrequencyType? InterestRateType,
    LoanInterestMethod? InterestMethod, LoanAmortizationMethod? AmortizationMethod, LoanStatus Status,
    int? ApprovedById, DateOnly? ApprovedDate, string? ApprovedNotes,
    int? NeedChangesById, DateOnly? NeedChangesDate,
    DateOnly? DisbursementDate, int? DisbursedById, string? DisbursedNotes,
    DateOnly? WrittenOffDate, string? WrittenOffNotes,
    bool IsNpa, bool IncomeSuspended, string? Notes);

/// <summary>FR-LN-8's bare disbursement transition — see LoanService.DisburseAsync's doc comment for what this intentionally does not do (no schedule generation).</summary>
public record DisburseLoanRequest(DateOnly? DisbursementDate, decimal DisbursedAmount, string? Notes);

// ---- Loan Charges ----

/// <summary>
/// Amount is entered directly (no automatic percentage/installment-based calculation engine —
/// that needs the schedule the still-deferred FR-LN-15 spec would provide).
/// </summary>
public record LoanChargeResponse(
    int Id, int? LoanId, int? ChargeId, bool Penalty, bool Waived,
    LoanChargeType ChargeType, LoanChargeCalculationType ChargeOption,
    decimal? Amount, decimal? AmountPaid, DateOnly? DueDate, int GracePeriod);

public record SaveLoanChargeRequest(
    int? ChargeId, bool Penalty, LoanChargeType ChargeType, LoanChargeCalculationType ChargeOption,
    decimal Amount, DateOnly? DueDate, int GracePeriod);
