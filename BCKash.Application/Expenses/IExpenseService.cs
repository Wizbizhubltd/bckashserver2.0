using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

public enum ExpenseWriteOutcome
{
    Success,
    NotFound,
    TypeNotFound,

    /// <summary>Only a Pending expense can be edited, approved or declined (mirrors LoanApplication's state machine).</summary>
    InvalidTransition,

    ReasonRequired,
}

public record ExpenseWriteResult(ExpenseWriteOutcome Outcome, Expense? Expense = null);

/// <summary>
/// Expense CRUD plus its approval workflow and recurrence (BR-EXP-1, FR-EXP-1). New expenses
/// always start Pending — GL posting only happens on approval, via
/// <see cref="IExpenseGlPostingService"/>.
/// </summary>
public interface IExpenseService
{
    Task<ExpenseWriteResult> CreateAsync(Expense expense, CancellationToken cancellationToken = default);

    Task<ExpenseWriteResult> UpdateAsync(int id, Expense updated, CancellationToken cancellationToken = default);

    Task<ExpenseWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ExpenseWriteResult> ApproveAsync(int id, string? notes, CancellationToken cancellationToken = default);

    Task<ExpenseWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances every recurring expense whose RecurNextDate is due (&lt;= today), cloning a new
    /// Pending occurrence dated on the due date and moving the original's RecurNextDate forward
    /// (FR-EXP-1). No scheduler exists in this codebase, so this is admin-triggered.
    /// </summary>
    Task<int> GenerateDueRecurringAsync(CancellationToken cancellationToken = default);
}
