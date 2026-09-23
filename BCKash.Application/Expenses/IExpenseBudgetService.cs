using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

public enum ExpenseBudgetWriteOutcome
{
    Success,
    NotFound,
    InvalidTransition,
    ReasonRequired,
}

public record ExpenseBudgetWriteResult(ExpenseBudgetWriteOutcome Outcome, ExpenseBudget? Budget = null);

/// <summary>Expense budget CRUD plus its own approval workflow (BR-EXP-2, FR-EXP-2). New budgets always start Pending.</summary>
public interface IExpenseBudgetService
{
    Task<ExpenseBudgetWriteResult> CreateAsync(ExpenseBudget budget, CancellationToken cancellationToken = default);

    Task<ExpenseBudgetWriteResult> UpdateAsync(int id, ExpenseBudget updated, CancellationToken cancellationToken = default);

    Task<ExpenseBudgetWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ExpenseBudgetWriteResult> ApproveAsync(int id, CancellationToken cancellationToken = default);

    Task<ExpenseBudgetWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);
}
