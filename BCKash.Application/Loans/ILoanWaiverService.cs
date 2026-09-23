using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanWaiverOutcome
{
    Success,
    ScheduleNotFound,
    InvalidAmount,
    ReasonRequired,
}

public record LoanWaiverResult(LoanWaiverOutcome Outcome, LoanRepaymentSchedule? Schedule = null);

/// <summary>FR-LN-20: waiving interest or a specific charge component on one schedule line.</summary>
public interface ILoanWaiverService
{
    Task<LoanWaiverResult> WaiveAsync(int scheduleId, LoanRepaymentComponent component, decimal amount, string reason, CancellationToken cancellationToken = default);
}
