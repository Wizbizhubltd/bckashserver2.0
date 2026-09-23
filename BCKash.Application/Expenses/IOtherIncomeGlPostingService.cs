using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

/// <summary>
/// GL posting hook for approved other income (BR-EXP-3, FR-EXP-3). Mirror image of
/// <see cref="IExpenseGlPostingService"/> — see its doc comment for the shared shape.
/// </summary>
public interface IOtherIncomeGlPostingService
{
    Task PostApprovalAsync(OtherIncome income, CancellationToken cancellationToken = default);
}
