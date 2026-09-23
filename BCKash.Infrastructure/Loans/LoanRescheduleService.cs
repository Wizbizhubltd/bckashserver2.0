using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// FR-LN-23. On approval, every schedule line due on/after <c>RescheduleFromDate</c> is replaced
/// (removed and regenerated via <see cref="LoanScheduleGenerator"/>) against the request's new
/// principal, keeping the same number of remaining installments and the loan's existing interest
/// rate/method — <c>RecalculateInterest</c> is captured but doesn't change the calculation itself
/// (this implementation always recalculates via the generator; there's no separate "keep the old
/// interest schedule, just push dates out" mode). Lines already due before that date are left
/// untouched.
/// </summary>
public class LoanRescheduleService : ILoanRescheduleService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public LoanRescheduleService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<LoanRescheduleResult> RequestAsync(int loanId, decimal principal, DateOnly rescheduleFromDate, bool recalculateInterest, string? notes, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.LoanNotFound);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new LoanRescheduleRequest
        {
            Loan = loan,
            Principal = principal,
            Status = RescheduleRequestStatus.Pending,
            RescheduleFromDate = rescheduleFromDate,
            RecalculateInterest = recalculateInterest ? 1 : 0,
            Notes = notes,
            CreatedById = _currentUser.UserId,
            CreatedDate = today,
        };
        _db.LoanRescheduleRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoanRescheduleResult(LoanRescheduleOutcome.Success, request);
    }

    public async Task<LoanRescheduleResult> ApproveAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var request = await _db.LoanRescheduleRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.NotFound);
        }

        if (request.Status != RescheduleRequestStatus.Pending)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.InvalidTransition);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == request.LoanId, cancellationToken);
        if (loan is null)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.LoanNotFound);
        }

        if (!LoanTransitionRules.HasActiveSchedule(loan.Status))
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.InvalidTransition);
        }

        var rescheduleFromDate = request.RescheduleFromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var futureLines = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loan.Id && s.DueDate != null && s.DueDate >= rescheduleFromDate)
            .OrderBy(s => s.DueDate)
            .ToListAsync(cancellationToken);

        if (futureLines.Count == 0)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.NothingToReschedule);
        }

        var lastPastInstallmentNumber = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loan.Id && s.DueDate != null && s.DueDate < rescheduleFromDate)
            .Select(s => (int?)s.Installment)
            .MaxAsync(cancellationToken) ?? 0;

        var repaymentFrequencyType = loan.RepaymentFrequencyType ?? FrequencyType.Months;
        var repaymentFrequency = loan.RepaymentFrequency ?? 1;
        var newPrincipal = request.Principal ?? loan.Principal ?? 0m;

        var scheduleInput = new ScheduleGenerationInput(
            Principal: newPrincipal,
            InterestRate: loan.InterestRate ?? 0m,
            InterestRateType: loan.InterestRateType ?? InterestRateFrequencyType.Year,
            LoanTerm: futureLines.Count * repaymentFrequency,
            LoanTermType: repaymentFrequencyType,
            RepaymentFrequency: repaymentFrequency,
            RepaymentFrequencyType: repaymentFrequencyType,
            InterestMethod: loan.InterestMethod ?? LoanInterestMethod.Flat,
            AmortizationMethod: loan.AmortizationMethod ?? LoanAmortizationMethod.EqualInstallment,
            CalculationPeriodType: InterestCalculationPeriodType.Same,
            YearDays: YearDaysType.Days365,
            MonthDays: MonthDaysType.Days30,
            GraceOnPrincipal: 0,
            GraceOnInterestCharged: 0,
            GraceOnInterestPayment: 0,
            DisbursementDate: SubtractPeriod(rescheduleFromDate, repaymentFrequencyType, repaymentFrequency));

        var newInstallments = LoanScheduleGenerator.Generate(scheduleInput);

        _db.LoanRepaymentSchedules.RemoveRange(futureLines);

        var installmentNumber = lastPastInstallmentNumber;
        foreach (var installment in newInstallments)
        {
            installmentNumber++;
            _db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Loan = loan,
                Installment = installmentNumber,
                DueDate = installment.DueDate,
                Principal = installment.Principal,
                Interest = installment.Interest,
                TotalDue = installment.Principal + installment.Interest,
                PrincipalPaid = 0m,
                InterestPaid = 0m,
                FeesPaid = 0m,
                PenaltyPaid = 0m,
                Paid = false,
                CreatedById = _currentUser.UserId,
            });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        request.Status = RescheduleRequestStatus.Approved;
        request.ApprovedById = _currentUser.UserId;
        request.ApprovedDate = today;

        loan.Status = LoanStatus.Rescheduled;
        loan.RescheduledById = _currentUser.UserId;
        loan.RescheduledDate = today;
        loan.RescheduledNotes = request.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanRescheduleResult(LoanRescheduleOutcome.Success, request);
    }

    public async Task<LoanRescheduleResult> RejectAsync(int requestId, string? notes, CancellationToken cancellationToken = default)
    {
        var request = await _db.LoanRescheduleRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.NotFound);
        }

        if (request.Status != RescheduleRequestStatus.Pending)
        {
            return new LoanRescheduleResult(LoanRescheduleOutcome.InvalidTransition);
        }

        request.Status = RescheduleRequestStatus.Rejected;
        request.RejectedById = _currentUser.UserId;
        request.RejectedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (notes is not null)
        {
            request.Notes = notes;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanRescheduleResult(LoanRescheduleOutcome.Success, request);
    }

    private static DateOnly SubtractPeriod(DateOnly date, FrequencyType type, int multiplier) => type switch
    {
        FrequencyType.Days => date.AddDays(-multiplier),
        FrequencyType.Weeks => date.AddDays(-multiplier * 7),
        FrequencyType.Months => date.AddMonths(-multiplier),
        FrequencyType.Years => date.AddYears(-multiplier),
        _ => date.AddDays(-multiplier),
    };
}
