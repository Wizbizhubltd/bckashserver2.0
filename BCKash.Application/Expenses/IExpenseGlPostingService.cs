using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

/// <summary>
/// GL posting hook for approved expenses (BR-EXP-1, FR-EXP-1). Loads the expense's type, builds
/// balanced posting lines via <see cref="BCKash.Domain.GeneralLedger.ExpenseGlPostingRules"/>,
/// and — unless the expense's date falls on/before an active GL closure for its office, or the
/// type isn't fully configured for GL — writes one
/// <see cref="BCKash.Domain.GeneralLedger.GlJournalEntry"/> row per line.
/// </summary>
public interface IExpenseGlPostingService
{
    Task PostApprovalAsync(Expense expense, CancellationToken cancellationToken = default);
}
