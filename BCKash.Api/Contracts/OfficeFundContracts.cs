using BCKash.Domain.Organization;

namespace BCKash.Api.Contracts;

public record OfficeBankAccountResponse(int Id, string BankName, string AccountName, string AccountNumber, bool IsDefault, bool Active, DateTime? CreatedAt);

/// <summary>Where a loan's repayments are paid in: its office's default bank account, or null when the office hasn't set one.</summary>
/// <summary>
/// Where a loan's repayments are paid in: its office's default bank account (null fields when the office hasn't
/// set one). <c>MaxRepayment</c>: the most that can be recorded now — what's still owed, less repayments
/// already waiting for confirmation.
/// </summary>
public record RepaymentAccountResponse(string? OfficeName, string? BankName, string? AccountName, string? AccountNumber, decimal? MaxRepayment = null);

public record OfficeFundingResponse(
    int Id, int OfficeId, string? OfficeName, decimal Amount, string Reference, DateOnly FundedOn, string? BankAccountLabel, string? Notes,
    OfficeFundingStatus Status, string? FundedByName, DateTime? CreatedAt,
    string? AcknowledgedByName, DateTime? AcknowledgedAt,
    string? DisputedByName, DateTime? DisputedAt, string? DisputeReason, string? DisputeDocumentName,
    string? CancelledByName, DateTime? CancelledAt, string? CancelReason);

public record OfficeFundEntryResponse(int Id, OfficeFundEntryType Type, decimal Amount, decimal BalanceAfter, int? FundingId, int? LoanId, string? Description, string? CreatedByName, DateTime CreatedAt);

public record OfficeFundEventResponse(int Id, OfficeFundEventType Type, int? FundingId, decimal? Amount, string? Comment, string? ActorName, DateTime CreatedAt);

/// <summary>Everything on an office's Business Operations tab.</summary>
public record OfficeBusinessOperationsResponse(
    int OfficeId, decimal Balance, decimal Committed, decimal Available, decimal PendingAmount, int PendingCount,
    int? ManagerId, string? ManagerName, bool LoansRequireFunds, bool CanAcknowledge,
    IReadOnlyList<OfficeBankAccountResponse> BankAccounts,
    IReadOnlyList<OfficeFundingResponse> Fundings,
    IReadOnlyList<OfficeFundEntryResponse> Entries,
    IReadOnlyList<OfficeFundEventResponse> Activity);

public record SaveOfficeBankAccountRequest(string? BankName, string? AccountName, string? AccountNumber, bool MakeDefault);

public record FundOfficeRequest(decimal Amount, string? Reference, DateOnly? FundedOn, string? Notes);

public record FundingCommentRequest(string? Comment);

public record BankResponse(string Name, string Category);

public record FundingTotal(decimal Amount, int Count);

/// <summary>Office funding across every office. <c>Total</c> is everything sent that wasn't cancelled.</summary>
public record OfficeFundingTotalsResponse(FundingTotal Total, FundingTotal Acknowledged, FundingTotal Disputed, FundingTotal Pending, FundingTotal Cancelled);
