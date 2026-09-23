using BCKash.Domain.Expenses;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for approved expenses (BR-EXP-1, FR-EXP-3/FR-GL-2-style).
/// Debits the expense type's configured expense account and credits its asset (cash/fund
/// source) account. Standard double-entry, not extracted from or validated against the legacy
/// PHP codebase (same caveat as docs/gl-posting-spec.md). Returns an empty list — posting
/// skipped, not partial — when the expense type isn't fully configured for GL.
/// </summary>
public static class ExpenseGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForApproval(ExpenseType expenseType, decimal amount)
    {
        if (amount <= 0 || expenseType.GlAccountExpenseId is not int expense || expenseType.GlAccountAssetId is not int asset)
        {
            return [];
        }

        return
        [
            new GlPostingLine(expense, Debit: amount, Credit: null),
            new GlPostingLine(asset, Debit: null, Credit: amount),
        ];
    }
}
