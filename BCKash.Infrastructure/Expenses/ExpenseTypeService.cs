using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class ExpenseTypeService : IExpenseTypeService
{
    private readonly BCKashDbContext _db;

    public ExpenseTypeService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<ExpenseTypeWriteResult> CreateAsync(ExpenseType expenseType, CancellationToken cancellationToken = default)
    {
        _db.ExpenseTypes.Add(expenseType);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.Success, expenseType);
    }

    public async Task<ExpenseTypeWriteResult> UpdateAsync(int id, ExpenseType updated, CancellationToken cancellationToken = default)
    {
        var expenseType = await _db.ExpenseTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (expenseType is null)
        {
            return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.NotFound);
        }

        expenseType.Name = updated.Name;
        expenseType.GlAccountAssetId = updated.GlAccountAssetId;
        expenseType.GlAccountExpenseId = updated.GlAccountExpenseId;
        expenseType.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.Success, expenseType);
    }

    public async Task<ExpenseTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var expenseType = await _db.ExpenseTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (expenseType is null)
        {
            return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.NotFound);
        }

        var inUse = await _db.Expenses.AnyAsync(e => e.ExpenseTypeId == id, cancellationToken)
            || await _db.ExpenseBudgets.AnyAsync(b => b.ExpenseTypeId == id, cancellationToken);
        if (inUse)
        {
            return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.InUse, expenseType);
        }

        _db.ExpenseTypes.Remove(expenseType);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseTypeWriteResult(ExpenseTypeWriteOutcome.Success);
    }
}
