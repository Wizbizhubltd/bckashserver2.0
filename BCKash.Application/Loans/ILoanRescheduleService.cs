using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanRescheduleOutcome
{
    Success,
    NotFound,
    LoanNotFound,
    InvalidTransition,
    NothingToReschedule,
}

public record LoanRescheduleResult(LoanRescheduleOutcome Outcome, LoanRescheduleRequest? Request = null);

/// <summary>FR-LN-23: reschedule request workflow. Approval regenerates the schedule from RescheduleFromDate forward — see LoanRescheduleService's doc comment for exactly how.</summary>
public interface ILoanRescheduleService
{
    Task<LoanRescheduleResult> RequestAsync(int loanId, decimal principal, DateOnly rescheduleFromDate, bool recalculateInterest, string? notes, CancellationToken cancellationToken = default);

    Task<LoanRescheduleResult> ApproveAsync(int requestId, CancellationToken cancellationToken = default);

    Task<LoanRescheduleResult> RejectAsync(int requestId, string? notes, CancellationToken cancellationToken = default);
}
