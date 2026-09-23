using BCKash.Application.Expenses;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.Expenses;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class ExpenseGlPostingService : IExpenseGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public ExpenseGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostApprovalAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var expenseType = await _db.ExpenseTypes.FirstOrDefaultAsync(t => t.Id == expense.ExpenseTypeId, cancellationToken);
        if (expenseType is null)
        {
            return;
        }

        var lines = ExpenseGlPostingRules.ForApproval(expenseType, expense.Amount);
        if (lines.Count == 0)
        {
            return;
        }

        var date = expense.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(expense.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"EXPENSE-{expense.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = expense.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = GlTransactionType.Expense,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                TransactionId = expense.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
