using BCKash.Domain.Expenses;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for approved other (non-lending) income (BR-EXP-3,
/// FR-EXP-3). Debits the income type's configured asset (cash/fund) account and credits its
/// income account — the mirror image of <see cref="ExpenseGlPostingRules"/>. Returns an empty
/// list — posting skipped, not partial — when the type isn't fully configured for GL.
/// </summary>
public static class OtherIncomeGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForApproval(OtherIncomeType incomeType, decimal amount)
    {
        if (amount <= 0 || incomeType.GlAccountAssetId is not int asset || incomeType.GlAccountIncomeId is not int income)
        {
            return [];
        }

        return
        [
            new GlPostingLine(asset, Debit: amount, Credit: null),
            new GlPostingLine(income, Debit: null, Credit: amount),
        ];
    }
}
