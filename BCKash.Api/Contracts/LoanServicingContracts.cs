using BCKash.Domain.Loans;

namespace BCKash.Api.Contracts;

// ---- Schedule (FR-LN-12 to FR-LN-14) ----

public record ScheduleInstallmentResponse(
    int Id, int? Installment, DateOnly? DueDate,
    decimal? Principal, decimal? PrincipalPaid, decimal? PrincipalWaived, decimal? PrincipalWrittenOff,
    decimal? Interest, decimal? InterestPaid, decimal? InterestWaived, decimal? InterestWrittenOff,
    decimal? Fees, decimal? FeesPaid, decimal? Penalty, decimal? PenaltyPaid,
    decimal? TotalDue, bool Paid);

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
