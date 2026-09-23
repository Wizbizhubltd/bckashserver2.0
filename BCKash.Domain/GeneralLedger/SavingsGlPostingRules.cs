using BCKash.Domain.Savings;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for savings transactions (BR-GL-2, FR-GL-2 extended to
/// savings in Phase 7). Same balanced-or-empty contract as <see cref="LoanGlPostingRules"/>:
/// <c>SavingsReference</c> is read as the cash/fund-side account (debited on deposit, credited
/// on withdrawal) and <c>SavingsControl</c> as the depositor-liability control account —
/// neither role is confirmed against a legacy source; see docs/savings-interest-spec.md for the
/// full caveat.
/// </summary>
public static class SavingsGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForDeposit(SavingsProduct product, decimal amount)
    {
        if (amount <= 0 || product.GlAccountSavingsReferenceId is not int reference || product.GlAccountSavingsControlId is not int control)
        {
            return [];
        }

        return
        [
            new GlPostingLine(reference, Debit: amount, Credit: null),
            new GlPostingLine(control, Debit: null, Credit: amount),
        ];
    }

    public static IReadOnlyList<GlPostingLine> ForWithdrawal(SavingsProduct product, decimal amount)
    {
        if (amount <= 0 || product.GlAccountSavingsReferenceId is not int reference || product.GlAccountSavingsControlId is not int control)
        {
            return [];
        }

        return
        [
            new GlPostingLine(control, Debit: amount, Credit: null),
            new GlPostingLine(reference, Debit: null, Credit: amount),
        ];
    }

    public static IReadOnlyList<GlPostingLine> ForInterest(SavingsProduct product, decimal amount)
    {
        if (amount <= 0 || product.GlAccountInterestOnSavingsId is not int interestExpense || product.GlAccountSavingsControlId is not int control)
        {
            return [];
        }

        return
        [
            new GlPostingLine(interestExpense, Debit: amount, Credit: null),
            new GlPostingLine(control, Debit: null, Credit: amount),
        ];
    }

    public static IReadOnlyList<GlPostingLine> ForCharge(SavingsProduct product, bool isPenalty, decimal amount)
    {
        var incomeAccount = isPenalty ? product.GlAccountIncomePenaltyId : product.GlAccountIncomeFeeId;
        if (amount <= 0 || product.GlAccountSavingsControlId is not int control || incomeAccount is not int income)
        {
            return [];
        }

        return
        [
            new GlPostingLine(control, Debit: amount, Credit: null),
            new GlPostingLine(income, Debit: null, Credit: amount),
        ];
    }
}
