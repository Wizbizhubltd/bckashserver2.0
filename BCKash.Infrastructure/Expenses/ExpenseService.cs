using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IExpenseGlPostingService _glPostingService;

    public ExpenseService(BCKashDbContext db, ICurrentUserContext currentUser, IExpenseGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<ExpenseWriteResult> CreateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        if (expense.ExpenseTypeId is int typeId && !await _db.ExpenseTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.TypeNotFound);
        }

        expense.CreatedById = _currentUser.UserId;
        expense.Status = ApprovalStatus.Pending;

        if (expense.Recurring && expense.RecurNextDate is null)
        {
            expense.RecurNextDate = expense.RecurStartDate ?? expense.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseWriteResult(ExpenseWriteOutcome.Success, expense);
    }

    public async Task<ExpenseWriteResult> UpdateAsync(int id, Expense updated, CancellationToken cancellationToken = default)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.NotFound);
        }

        if (expense.Status != ApprovalStatus.Pending)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.InvalidTransition);
        }

        if (updated.ExpenseTypeId is int typeId && !await _db.ExpenseTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.TypeNotFound);
        }

        expense.OfficeId = updated.OfficeId;
        expense.ExpenseTypeId = updated.ExpenseTypeId;
        expense.Name = updated.Name;
        expense.Amount = updated.Amount;
        expense.Date = updated.Date;
        expense.Year = updated.Year;
        expense.Month = updated.Month;
        expense.Recurring = updated.Recurring;
        expense.RecurFrequency = updated.RecurFrequency;
        expense.RecurStartDate = updated.RecurStartDate;
        expense.RecurEndDate = updated.RecurEndDate;
        expense.RecurNextDate = updated.RecurNextDate ?? expense.RecurNextDate;
        expense.RecurType = updated.RecurType;
        expense.Notes = updated.Notes;
        expense.Files = updated.Files;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseWriteResult(ExpenseWriteOutcome.Success, expense);
    }

    public async Task<ExpenseWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.NotFound);
        }

        if (expense.Status != ApprovalStatus.Pending)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.InvalidTransition);
        }

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseWriteResult(ExpenseWriteOutcome.Success);
    }

    public async Task<ExpenseWriteResult> ApproveAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.NotFound);
        }

        if (expense.Status != ApprovalStatus.Pending)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.InvalidTransition);
        }

        expense.Status = ApprovalStatus.Approved;
        expense.ApprovedById = _currentUser.UserId;
        expense.ApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (notes is not null)
        {
            expense.Notes = notes;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostApprovalAsync(expense, cancellationToken);

        return new ExpenseWriteResult(ExpenseWriteOutcome.Success, expense);
    }

    public async Task<ExpenseWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.ReasonRequired);
        }

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.NotFound);
        }

        if (expense.Status != ApprovalStatus.Pending)
        {
            return new ExpenseWriteResult(ExpenseWriteOutcome.InvalidTransition);
        }

        expense.Status = ApprovalStatus.Declined;
        expense.DeclinedById = _currentUser.UserId;
        expense.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        expense.Notes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ExpenseWriteResult(ExpenseWriteOutcome.Success, expense);
    }

    public async Task<int> GenerateDueRecurringAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await _db.Expenses
            .Where(e => e.Recurring && e.RecurNextDate != null && e.RecurNextDate <= today)
            .Where(e => e.RecurEndDate == null || e.RecurNextDate <= e.RecurEndDate)
            .ToListAsync(cancellationToken);

        var generated = 0;
        foreach (var template in due)
        {
            var occurrenceDate = template.RecurNextDate!.Value;

            var occurrence = new Expense
            {
                OfficeId = template.OfficeId,
                CreatedById = template.CreatedById,
                ExpenseTypeId = template.ExpenseTypeId,
                Name = template.Name,
                Amount = template.Amount,
                Date = occurrenceDate,
                Year = occurrenceDate.Year.ToString(),
                Month = occurrenceDate.Month.ToString(),
                Recurring = false,
                Status = ApprovalStatus.Pending,
                Notes = template.Notes,
            };
            _db.Expenses.Add(occurrence);
            generated++;

            var nextDate = ExpenseRecurrenceRules.NextDate(occurrenceDate, template.RecurType, template.RecurFrequency);
            if (template.RecurEndDate is DateOnly end && nextDate > end)
            {
                template.Recurring = false;
                template.RecurNextDate = null;
            }
            else
            {
                template.RecurNextDate = nextDate;
            }
        }

        if (generated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return generated;
    }
}
