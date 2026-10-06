using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>See <see cref="ILoanCompletionService"/>.</summary>
public class LoanCompletionService : ILoanCompletionService
{
    /// <summary>Marks the loans this service closed, so a reversal only ever reopens those.</summary>
    public const string AutoClosedNote = "Fully repaid — closed automatically.";

    // Loans being repaid. A List so EF can translate Contains.
    private static readonly List<LoanStatus> BeingRepaid = [LoanStatus.Disbursed, LoanStatus.Rescheduled];

    // The sweep checks loans this many at a time, so memory stays at one batch rather than every active loan.
    private const int SweepBatchSize = 500;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public LoanCompletionService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CompleteIfSettledAsync(int loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null || !BeingRepaid.Contains(loan.Status))
        {
            return false;
        }

        var schedule = await _db.LoanRepaymentSchedules.Where(s => s.LoanId == loanId).ToListAsync(cancellationToken);
        if (schedule.Count == 0 || schedule.Sum(Owed) > 0)
        {
            return false;
        }

        loan.Status = LoanStatus.Closed;
        loan.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        loan.ClosedById = _currentUser.UserId;
        loan.ClosedNotes = AutoClosedNote;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReopenIfOwingAsync(int loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is not { Status: LoanStatus.Closed, ClosedNotes: AutoClosedNote })
        {
            return;
        }

        var schedule = await _db.LoanRepaymentSchedules.Where(s => s.LoanId == loanId).ToListAsync(cancellationToken);
        if (schedule.Sum(Owed) <= 0)
        {
            return;
        }

        // Back to how it was being repaid before it closed.
        var rescheduled = await _db.LoanRescheduleRequests.AnyAsync(r => r.LoanId == loanId && r.Status == RescheduleRequestStatus.Approved, cancellationToken);
        loan.Status = rescheduled ? LoanStatus.Rescheduled : LoanStatus.Disbursed;
        loan.ClosedDate = null;
        loan.ClosedById = null;
        loan.ClosedNotes = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _db.Loans
            .NotCacheable()
            .Where(l => BeingRepaid.Contains(l.Status) && l.DeletedAt == null && _db.LoanRepaymentSchedules.Any(s => s.LoanId == l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var closed = 0;
        foreach (var batch in candidates.Chunk(SweepBatchSize))
        {
            // Untracked and past the second-level cache: almost every loan still owes, so only the
            // settled ones are loaded again (tracked) by CompleteIfSettledAsync to be closed.
            var settled = (await _db.LoanRepaymentSchedules
                    .AsNoTracking()
                    .NotCacheable()
                    .Where(s => s.LoanId.HasValue && batch.Contains(s.LoanId.Value))
                    .ToListAsync(cancellationToken))
                .GroupBy(s => s.LoanId!.Value)
                .Where(g => g.Sum(Owed) <= 0)
                .Select(g => g.Key)
                .ToList();

            foreach (var loanId in settled)
            {
                if (await CompleteIfSettledAsync(loanId, cancellationToken))
                {
                    closed++;
                }
            }

            _db.ChangeTracker.Clear();
        }

        return closed;
    }

    /// <summary>What's still owed on an instalment: everything due, less what's been paid, waived or written off.</summary>
    public static decimal Owed(LoanRepaymentSchedule s) =>
        Remaining(s.Principal, s.PrincipalPaid, s.PrincipalWaived, s.PrincipalWrittenOff)
        + Remaining(s.Interest, s.InterestPaid, s.InterestWaived, s.InterestWrittenOff)
        + Remaining(s.Fees, s.FeesPaid, s.FeesWaived, s.FeesWrittenOff)
        + Remaining(s.Penalty, s.PenaltyPaid, s.PenaltyWaived, s.PenaltyWrittenOff);

    public static decimal Remaining(decimal? due, decimal? paid, decimal? waived, decimal? writtenOff) =>
        Math.Max(0, (due ?? 0) - (paid ?? 0) - (waived ?? 0) - (writtenOff ?? 0));
}
