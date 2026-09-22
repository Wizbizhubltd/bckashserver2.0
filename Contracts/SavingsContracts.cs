using BCKash.Domain.Savings;

namespace BCKash.Api.Contracts;

// ---- Savings Products ----

public record SavingsProductResponse(
    int Id, string? Name, string? ShortName, string? Description, int? CurrencyId, int Decimals,
    decimal? InterestRate, bool AllowOverdraft, decimal? MinimumBalance,
    InterestCompoundingPeriod? InterestCompoundingPeriod, InterestPostingPeriod? InterestPostingPeriod,
    InterestCalculationType? InterestCalculationType, bool AllowTransferWithdrawalFee, decimal? OpeningBalance,
    bool AllowAdditionalCharges, SavingsYearDays YearDays, SavingsAccountingRule AccountingRule,
    int? GlAccountSavingsReferenceId, int? GlAccountOverdraftPortfolioId, int? GlAccountSavingsControlId,
    int? GlAccountInterestOnSavingsId, int? GlAccountSavingsWrittenOffId, int? GlAccountIncomeInterestId,
    int? GlAccountIncomeFeeId, int? GlAccountIncomePenaltyId, bool Active);

public record SaveSavingsProductRequest(
    string? Name, string? ShortName, string? Description, int? CurrencyId, int Decimals,
    decimal? InterestRate, bool AllowOverdraft, decimal? MinimumBalance,
    InterestCompoundingPeriod? InterestCompoundingPeriod, InterestPostingPeriod? InterestPostingPeriod,
    InterestCalculationType? InterestCalculationType, bool AllowTransferWithdrawalFee, decimal? OpeningBalance,
    bool AllowAdditionalCharges, SavingsYearDays YearDays, SavingsAccountingRule AccountingRule,
    int? GlAccountSavingsReferenceId, int? GlAccountOverdraftPortfolioId, int? GlAccountSavingsControlId,
    int? GlAccountInterestOnSavingsId, int? GlAccountSavingsWrittenOffId, int? GlAccountIncomeInterestId,
    int? GlAccountIncomeFeeId, int? GlAccountIncomePenaltyId);

// ---- Savings Accounts ----

public record SavingsAccountResponse(
    int Id, SavingsClientType ClientType, int ClientId, int? GroupId, int? OfficeId, int? SavingsProductId,
    string? AccountNumber, int? CurrencyId, decimal? InterestRate, bool AllowOverdraft, decimal? MinimumBalance,
    decimal? OverdraftLimit, SavingsAccountStatus Status, decimal? Balance, decimal? Deposits, decimal? Withdrawals,
    decimal? InterestEarned, decimal? InterestPosted, DateOnly? NextInterestCalculationDate, DateOnly? NextInterestPostingDate,
    string? Notes);

public record OpenSavingsAccountRequest(SavingsClientType ClientType, int ClientId, int? GroupId, int? OfficeId, int? SavingsProductId, string? Notes);

public record ApproveSavingsAccountRequest(decimal? OpeningBalance, decimal? OverdraftLimit, DateOnly? Date, string? Notes);

public record DeclineSavingsAccountRequest(string Reason);

// ---- Savings Transactions ----

public record SavingsTransactionResponse(
    int Id, int? SavingsId, SavingsTransactionType? TransactionType, decimal? Amount, decimal? Debit, decimal? Credit,
    decimal? Balance, bool Reversible, bool Reversed, DateOnly? Date, string? Notes);

public record RecordSavingsTransactionRequest(decimal Amount, DateOnly? Date, string? Notes);

// ---- Savings Charges ----

public record SavingsChargeResponse(
    int Id, int? SavingsId, SavingsChargeType ChargeType, bool Penalty, bool Waived, decimal? Amount, decimal? AmountPaid, DateOnly? DueDate);

public record AttachSavingsChargeRequest(SavingsChargeType ChargeType, bool Penalty, decimal Amount, DateOnly? DueDate);

// ---- Transfers ----

public record RepayLoanFromSavingsRequest(int SavingsId, int LoanId, decimal Amount, DateOnly? Date, string? Notes);

public record DisburseLoanToSavingsRequest(int LoanId, int SavingsId, decimal Amount, DateOnly? Date, string? Notes);
