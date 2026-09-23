using BCKash.Domain.GeneralLedger;

namespace BCKash.Api.Contracts;

// ---- Chart of accounts ----

public record GlAccountResponse(
    int Id, string? Name, int? ParentId, string? GlCode, GlAccountType AccountType, bool Active, bool ManualEntries, string? Notes);

public record SaveGlAccountRequest(string? Name, int? ParentId, string? GlCode, GlAccountType AccountType, bool ManualEntries, string? Notes);

// ---- Journal entries ----

public record GlJournalEntryResponse(
    int Id, int? OfficeId, int? GlAccountId, string? GlAccountName, GlTransactionType? TransactionType,
    GlTransactionSubType? TransactionSubType, decimal? Debit, decimal? Credit, bool Reversed, string? Reference,
    int? LoanId, int? LoanTransactionId, int? SavingsId, int? SavingsTransactionId, DateOnly? Date, string? Narration,
    bool ManualEntry, bool Approved, int? ApprovedById, DateOnly? ApprovedDate, string? ApprovedNotes);

public record JournalEntryLineRequest(int GlAccountId, decimal? Debit, decimal? Credit);

public record CreateManualJournalEntryRequest(int? OfficeId, DateOnly Date, IReadOnlyList<JournalEntryLineRequest> Lines, string Narration);

public record ApproveJournalEntryRequest(string? Notes);

// ---- Closures ----

public record GlClosureResponse(
    int Id, int? OfficeId, DateOnly ClosingDate, string? Notes, DateTime? ReopenedAt, int? ReopenedById);

public record CreateGlClosureRequest(int? OfficeId, DateOnly ClosingDate, string? Notes);

public record ReopenGlClosureRequest(string? Notes);

// ---- Office transfers ----

public record OfficeTransactionResponse(
    int Id, int? FromOfficeId, int? ToOfficeId, int? CurrencyId, decimal? Amount, DateOnly? Date, string? Notes);

public record CreateOfficeTransferRequest(
    int FromOfficeId, int ToOfficeId, int? CurrencyId, decimal Amount, int GlAccountId, DateOnly Date, string? Notes);

// ---- Reports ----

public record TrialBalanceRowResponse(int GlAccountId, string? Name, string? GlCode, string AccountType, decimal TotalDebit, decimal TotalCredit);

public record AccountTypeBalanceResponse(string AccountType, decimal TotalDebit, decimal TotalCredit, decimal Net);

public record ProfitAndLossResponse(IReadOnlyList<AccountTypeBalanceResponse> Sections, decimal NetProfit);

public record CashFlowPeriodResponse(string Period, decimal NetMovement);
