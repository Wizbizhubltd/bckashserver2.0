using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class ExpenseBudgetService : IExpenseBudgetService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ExpenseBudgetService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ExpenseBudgetWriteResult> CreateAsync(ExpenseBudget budget, CancellationToken cancellationToken = default)
    {
        budget.CreatedById = _currentUser.UserId;
        budget.Status = ApprovalStatus.Pending;

        _db.ExpenseBudgets.Add(budget);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.Success, budget);
    }

    public async Task<ExpenseBudgetWriteResult> UpdateAsync(int id, ExpenseBudget updated, CancellationToken cancellationToken = default)
    {
        var budget = await _db.ExpenseBudgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (budget is null)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.NotFound);
        }

        if (budget.Status != ApprovalStatus.Pending)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.InvalidTransition);
        }

        budget.OfficeId = updated.OfficeId;
        budget.ExpenseTypeId = updated.ExpenseTypeId;
        budget.Name = updated.Name;
        budget.Year = updated.Year;
        budget.Month = updated.Month;
        budget.Date = updated.Date;
        budget.Amount = updated.Amount;
        budget.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.Success, budget);
    }

    public async Task<ExpenseBudgetWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var budget = await _db.ExpenseBudgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (budget is null)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.NotFound);
        }

        if (budget.Status != ApprovalStatus.Pending)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.InvalidTransition);
        }

        _db.ExpenseBudgets.Remove(budget);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.Success);
    }

    public async Task<ExpenseBudgetWriteResult> ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        var budget = await _db.ExpenseBudgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (budget is null)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.NotFound);
        }

        if (budget.Status != ApprovalStatus.Pending)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.InvalidTransition);
        }

        budget.Status = ApprovalStatus.Approved;
        budget.ApprovedById = _currentUser.UserId;
        budget.ApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.Success, budget);
    }

    public async Task<ExpenseBudgetWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.ReasonRequired);
        }

        var budget = await _db.ExpenseBudgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (budget is null)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.NotFound);
        }

        if (budget.Status != ApprovalStatus.Pending)
        {
            return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.InvalidTransition);
        }

        budget.Status = ApprovalStatus.Declined;
        budget.DeclinedById = _currentUser.UserId;
        budget.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        budget.Notes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome.Success, budget);
    }
}
