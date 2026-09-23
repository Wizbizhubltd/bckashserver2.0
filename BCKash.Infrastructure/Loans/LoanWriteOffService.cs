using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>FR-LN-24. Recovery transactions are recorded against the loan directly, not mapped back onto individual schedule lines, since the debt those lines represented is already written off — there's nothing left on the schedule to allocate a recovery against.</summary>
public class LoanWriteOffService : ILoanWriteOffService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILoanGlPostingService _glPostingService;

    public LoanWriteOffService(BCKashDbContext db, ICurrentUserContext currentUser, ILoanGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<LoanWriteOffResult> WriteOffAsync(int loanId, string reason, DateOnly? date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.ReasonRequired);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.NotFound);
        }

        if (!LoanTransitionRules.CanWriteOff(loan.Status))
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.InvalidTransition);
        }

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var unpaidSchedules = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loanId && s.Paid == false)
            .ToListAsync(cancellationToken);

        decimal totalPrincipal = 0, totalInterest = 0, totalFees = 0, totalPenalty = 0;

        foreach (var schedule in unpaidSchedules)
        {
            var principalOut = Outstanding(schedule.Principal, schedule.PrincipalWaived, schedule.PrincipalWrittenOff, schedule.PrincipalPaid);
            var interestOut = Outstanding(schedule.Interest, schedule.InterestWaived, schedule.InterestWrittenOff, schedule.InterestPaid);
            var feesOut = Outstanding(schedule.Fees, schedule.FeesWaived, schedule.FeesWrittenOff, schedule.FeesPaid);
            var penaltyOut = Outstanding(schedule.Penalty, schedule.PenaltyWaived, schedule.PenaltyWrittenOff, schedule.PenaltyPaid);

            schedule.PrincipalWrittenOff = (schedule.PrincipalWrittenOff ?? 0m) + principalOut;
            schedule.InterestWrittenOff = (schedule.InterestWrittenOff ?? 0m) + interestOut;
            schedule.FeesWrittenOff = (schedule.FeesWrittenOff ?? 0m) + feesOut;
            schedule.PenaltyWrittenOff = (schedule.PenaltyWrittenOff ?? 0m) + penaltyOut;
            schedule.Paid = true;
            schedule.ModifiedById = _currentUser.UserId;

            totalPrincipal += principalOut;
            totalInterest += interestOut;
            totalFees += feesOut;
            totalPenalty += penaltyOut;
        }

        var transaction = new LoanTransaction
        {
            Loan = loan,
            OfficeId = loan.OfficeId,
            ClientId = loan.ClientId,
            TransactionType = LoanTransactionType.WriteOff,
            Amount = totalPrincipal + totalInterest + totalFees + totalPenalty,
            Debit = totalPrincipal + totalInterest + totalFees + totalPenalty,
            Principal = totalPrincipal,
            Interest = totalInterest,
            Fee = totalFees,
            Penalty = totalPenalty,
            Date = effectiveDate,
            Status = ApprovalStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = reason,
        };
        _db.LoanTransactions.Add(transaction);

        loan.Status = LoanStatus.WrittenOff;
        loan.WrittenOffById = _currentUser.UserId;
        loan.WrittenOffDate = effectiveDate;
        loan.WrittenOffNotes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostWriteOffAsync(loan, transaction, cancellationToken);
        return new LoanWriteOffResult(LoanWriteOffOutcome.Success, loan, transaction);
    }

    public async Task<LoanWriteOffResult> RecordRecoveryAsync(int loanId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.InvalidAmount);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.NotFound);
        }

        if (loan.Status != LoanStatus.WrittenOff)
        {
            return new LoanWriteOffResult(LoanWriteOffOutcome.InvalidTransition);
        }

        var transaction = new LoanTransaction
        {
            Loan = loan,
            OfficeId = loan.OfficeId,
            ClientId = loan.ClientId,
            TransactionType = LoanTransactionType.WriteOffRecovery,
            Amount = amount,
            Credit = amount,
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ApprovalStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = notes,
        };
        _db.LoanTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostWriteOffRecoveryAsync(loan, transaction, cancellationToken);
        return new LoanWriteOffResult(LoanWriteOffOutcome.Success, loan, transaction);
    }

    private static decimal Outstanding(decimal? due, decimal? waived, decimal? writtenOff, decimal? paid) =>
        Math.Max(0m, (due ?? 0m) - (waived ?? 0m) - (writtenOff ?? 0m) - (paid ?? 0m));
}
