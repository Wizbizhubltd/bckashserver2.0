using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanNpaService : ILoanNpaService
{
    private readonly BCKashDbContext _db;

    public LoanNpaService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<LoanNpaResult> RecomputeAsync(int loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new LoanNpaResult(LoanNpaOutcome.NotFound);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var oldestOverdueDueDate = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loanId && s.Paid == false && s.DueDate != null && s.DueDate < today)
            .OrderBy(s => s.DueDate)
            .Select(s => s.DueDate)
            .FirstOrDefaultAsync(cancellationToken);

        var daysInArrears = oldestOverdueDueDate.HasValue ? today.DayNumber - oldestOverdueDueDate.Value.DayNumber : 0;

        var isNpa = LoanNpaRules.IsNpa(daysInArrears, product?.NpaDays);
        var incomeSuspended = LoanNpaRules.ShouldSuspendIncome(isNpa, product?.NpaSuspendIncome ?? false);

        loan.IsNpa = isNpa;
        loan.IncomeSuspended = incomeSuspended;
        await _db.SaveChangesAsync(cancellationToken);

        return new LoanNpaResult(LoanNpaOutcome.Success, isNpa, incomeSuspended, daysInArrears);
    }
}
