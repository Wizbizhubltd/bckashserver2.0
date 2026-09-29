using BCKash.Domain.Loans;

namespace BCKash.Api.Contracts;

// ---- Schedule (FR-LN-12 to FR-LN-14) ----

public record ScheduleInstallmentResponse(
    int Id, int? Installment, DateOnly? DueDate,
    decimal? Principal, decimal? PrincipalPaid, decimal? PrincipalWaived, decimal? PrincipalWrittenOff,
    decimal? Interest, decimal? InterestPaid, decimal? InterestWaived, decimal? InterestWrittenOff,
    decimal? Fees, decimal? FeesPaid, decimal? Penalty, decimal? PenaltyPaid,
    decimal? TotalDue, bool Paid,
    // What the client pays for this instalment: TotalDue, grossed up on a savings loan (see ClientSavingsRules).
    decimal? CustomerPays = null);

// ---- Repayments (FR-LN-16 to FR-LN-19) ----

public record LoanTransactionResponse(
    int Id, int? LoanId, LoanTransactionType? TransactionType,
    decimal? Amount, decimal? Principal, decimal? Interest, decimal? Fee, decimal? Penalty, decimal? Overpayment,
    DateOnly? Date, bool Reversible, bool Reversed, string? Notes);

public record RecordRepaymentRequest(decimal Amount, int? PaymentTypeId, DateOnly? Date, string? Notes);

// ---- Waivers (FR-LN-20) ----

public record WaiveChargeRequest(int ScheduleId, LoanRepaymentComponent Component, decimal Amount, string Reason);

// ---- Reschedule (FR-LN-23) ----

public record LoanRescheduleRequestResponse(
    int Id, int? LoanId, decimal? Principal, RescheduleRequestStatus Status,
    DateOnly? RescheduleFromDate, bool RecalculateInterest, string? Notes,
    DateOnly? ApprovedDate, DateOnly? RejectedDate);

public record CreateRescheduleRequest(decimal Principal, DateOnly RescheduleFromDate, bool RecalculateInterest, string? Notes);

// ---- Write-off & recovery (FR-LN-24) ----

public record WriteOffLoanRequest(string Reason, DateOnly? Date);

public record RecordRecoveryRequest(decimal Amount, DateOnly? Date, string? Notes);

// ---- NPA (FR-LN-25) ----

public record NpaStatusResponse(bool IsNpa, bool IncomeSuspended, int DaysInArrears);

/// <summary>A repayment waiting for (or past) the office manager's confirmation. <c>CanReview</c>: whether the viewer may approve or dispute it.</summary>
public record RepaymentSubmissionResponse(
    int Id, int LoanId, decimal Amount, DateOnly? PaymentDate, string? Notes, RepaymentSubmissionStatus Status,
    string? SubmittedByName, DateTime? SubmittedAt, string? ReviewedByName, DateTime? ReviewedAt, string? DisputeReason,
    int? LoanTransactionId, bool CanReview);

/// <summary>
/// A loan at a glance. <c>ExpectedTotal</c> is principal plus interest (fees and penalties are shown
/// separately); <c>TotalRemaining</c> is what's still owed to the loan. On a savings loan the client pays a
/// little more — <c>CustomerTotal</c>/<c>CustomerRemaining</c> — because a share of each payment goes into
/// their savings. Amounts are null before the loan is disbursed and has a schedule.
/// </summary>
public record LoanSummaryResponse(
    decimal? Principal,
    decimal? Interest,
    decimal? ExpectedTotal,
    decimal? Fees,
    DateOnly? ExpectedCompletionDate,
    decimal TotalRepaid,
    decimal SavedFromRepayments,
    decimal? TotalRemaining,
    decimal? SavingsRate,
    decimal? CustomerTotal,
    decimal? CustomerRemaining,
    LoanPenaltySummaryResponse Penalty,
    LoanCompletionResponse? Completion = null);

/// <summary>
/// Whether the loan is completed — fully repaid and closed out — and if it isn't, what's still owed that keeps it
/// open, split by kind. <c>Completed</c> is false with nothing owed only before disbursement.
/// </summary>
public record LoanCompletionResponse(
    bool Completed, DateOnly? CompletedOn, decimal PrincipalOwed, decimal InterestOwed, decimal FeesOwed, decimal PenaltyOwed);

/// <summary>Penalties on the loan. <c>Status</c>: None, Unpaid, PartPaid, Paid or Waived.</summary>
public record LoanPenaltySummaryResponse(decimal Charged, decimal Paid, decimal Waived, decimal Outstanding, DateOnly? StartedOn, string Status);
