using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

public enum ExpenseTypeWriteOutcome
{
    Success,
    NotFound,
    InUse,
}

public record ExpenseTypeWriteResult(ExpenseTypeWriteOutcome Outcome, ExpenseType? ExpenseType = null);

/// <summary>Expense type ("product") CRUD — the GL-account configuration used by expense approval (BR-EXP-1). Mirrors ILoanProductService's shape.</summary>
public interface IExpenseTypeService
{
    Task<ExpenseTypeWriteResult> CreateAsync(ExpenseType expenseType, CancellationToken cancellationToken = default);

    Task<ExpenseTypeWriteResult> UpdateAsync(int id, ExpenseType updated, CancellationToken cancellationToken = default);

    Task<ExpenseTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
