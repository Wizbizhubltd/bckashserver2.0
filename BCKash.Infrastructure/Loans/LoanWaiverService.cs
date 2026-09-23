using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanWaiverService : ILoanWaiverService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILoanGlPostingService _glPostingService;

    public LoanWaiverService(BCKashDbContext db, ICurrentUserContext currentUser, ILoanGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<LoanWaiverResult> WaiveAsync(int scheduleId, LoanRepaymentComponent component, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new LoanWaiverResult(LoanWaiverOutcome.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new LoanWaiverResult(LoanWaiverOutcome.ReasonRequired);
        }

        var schedule = await _db.LoanRepaymentSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return new LoanWaiverResult(LoanWaiverOutcome.ScheduleNotFound);
        }

        switch (component)
        {
            case LoanRepaymentComponent.Principal:
                schedule.PrincipalWaived = (schedule.PrincipalWaived ?? 0m) + amount;
                break;
            case LoanRepaymentComponent.Interest:
                schedule.InterestWaived = (schedule.InterestWaived ?? 0m) + amount;
                break;
            case LoanRepaymentComponent.Fees:
                schedule.FeesWaived = (schedule.FeesWaived ?? 0m) + amount;
                break;
            case LoanRepaymentComponent.Penalty:
                schedule.PenaltyWaived = (schedule.PenaltyWaived ?? 0m) + amount;
                break;
        }

        schedule.ModifiedById = _currentUser.UserId;
        schedule.Paid = Outstanding(schedule.Principal, schedule.PrincipalWaived, schedule.PrincipalWrittenOff, schedule.PrincipalPaid) <= 0m
            && Outstanding(schedule.Interest, schedule.InterestWaived, schedule.InterestWrittenOff, schedule.InterestPaid) <= 0m
            && Outstanding(schedule.Fees, schedule.FeesWaived, schedule.FeesWrittenOff, schedule.FeesPaid) <= 0m
            && Outstanding(schedule.Penalty, schedule.PenaltyWaived, schedule.PenaltyWrittenOff, schedule.PenaltyPaid) <= 0m;

        var transaction = new LoanTransaction
        {
            LoanId = schedule.LoanId,
            LoanRepaymentSchedule = schedule,
            TransactionType = component == LoanRepaymentComponent.Interest ? LoanTransactionType.InterestWaiver : LoanTransactionType.ChargeWaiver,
            Amount = amount,
            Interest = component == LoanRepaymentComponent.Interest ? amount : null,
            Fee = component == LoanRepaymentComponent.Fees ? amount : null,
            Penalty = component == LoanRepaymentComponent.Penalty ? amount : null,
            Principal = component == LoanRepaymentComponent.Principal ? amount : null,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ApprovalStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = reason,
        };
        _db.LoanTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == schedule.LoanId, cancellationToken);
        if (loan is not null)
        {
            await _glPostingService.PostWaiverAsync(loan, transaction, component, amount, cancellationToken);
        }

        return new LoanWaiverResult(LoanWaiverOutcome.Success, schedule);
    }

    private static decimal Outstanding(decimal? due, decimal? waived, decimal? writtenOff, decimal? paid) =>
        Math.Max(0m, (due ?? 0m) - (waived ?? 0m) - (writtenOff ?? 0m) - (paid ?? 0m));
}
