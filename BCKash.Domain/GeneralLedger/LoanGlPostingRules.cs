using BCKash.Domain.Loans;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for loan transactions (BR-GL-2, FR-GL-2). The *account
/// roles* these methods post to come straight from <see cref="LoanProduct"/>'s twelve
/// `GlAccount*Id` fields (Phase 4), whose names are unambiguous. The *debit/credit posting
/// logic itself* — which side each component hits, how a write-off or a waiver is booked —
/// is standard MFI double-entry bookkeeping, not extracted from or validated against the
/// legacy PHP codebase (no legacy source was ever available, same situation as
/// docs/interest-calculation-spec.md's FR-LN-15 caveat). See docs/gl-posting-spec.md for the
/// full write-up and worked examples. Every method here is balanced by construction (debit
/// sum == credit sum for whatever lines it returns) and is a pure function — no I/O.
///
/// If a transaction's required account(s) aren't configured on the product, the method
/// returns an empty list rather than a partial/unbalanced posting — the caller
/// (<c>LoanGlPostingService</c>) treats an empty result as "posting skipped, product not
/// fully configured for GL," recorded rather than silently dropped.
/// </summary>
public static class LoanGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForDisbursement(LoanProduct product, decimal amount)
    {
        if (amount <= 0 || product.GlAccountLoanPortfolioId is not int portfolio || product.GlAccountFundSourceId is not int fundSource)
        {
            return [];
        }

        return
        [
            new GlPostingLine(portfolio, Debit: amount, Credit: null),
            new GlPostingLine(fundSource, Debit: null, Credit: amount),
        ];
    }

    public static IReadOnlyList<GlPostingLine> ForRepayment(
        LoanProduct product, decimal principal, decimal interest, decimal fees, decimal penalty, decimal overpayment)
    {
        if (product.GlAccountFundSourceId is not int fundSource)
        {
            return [];
        }

        return ForRepaymentDebitedFrom(product, fundSource, principal, interest, fees, penalty, overpayment);
    }

    /// <summary>
    /// Same credit-side lines as <see cref="ForRepayment"/> (loan portfolio/income interest/
    /// fee/penalty/overpayments), but the debit leg is caller-supplied instead of read from the
    /// product's fund-source account. Used by Phase 7's savings-funded repayment transfer,
    /// where no cash actually moves — the debit leg is the savings-side liability control
    /// account, not a cash/fund account. See docs/savings-interest-spec.md.
    /// </summary>
    public static IReadOnlyList<GlPostingLine> ForRepaymentFromSavings(
        LoanProduct product, int debitAccountId, decimal principal, decimal interest, decimal fees, decimal penalty, decimal overpayment) =>
        ForRepaymentDebitedFrom(product, debitAccountId, principal, interest, fees, penalty, overpayment);

    private static IReadOnlyList<GlPostingLine> ForRepaymentDebitedFrom(
        LoanProduct product, int debitAccountId, decimal principal, decimal interest, decimal fees, decimal penalty, decimal overpayment)
    {
        var total = principal + interest + fees + penalty + overpayment;
        if (total <= 0)
        {
            return [];
        }

        var lines = new List<GlPostingLine> { new(debitAccountId, Debit: total, Credit: null) };

        if (principal > 0)
        {
            if (product.GlAccountLoanPortfolioId is not int portfolio)
            {
                return [];
            }

            lines.Add(new GlPostingLine(portfolio, Debit: null, Credit: principal, GlTransactionSubType.RepaymentPrincipal));
        }

        if (interest > 0)
        {
            if (product.GlAccountIncomeInterestId is not int incomeInterest)
            {
                return [];
            }

            lines.Add(new GlPostingLine(incomeInterest, Debit: null, Credit: interest, GlTransactionSubType.RepaymentInterest));
        }

        if (fees > 0)
        {
            if (product.GlAccountIncomeFeeId is not int incomeFee)
            {
                return [];
            }

            lines.Add(new GlPostingLine(incomeFee, Debit: null, Credit: fees, GlTransactionSubType.RepaymentFees));
        }

        if (penalty > 0)
        {
            if (product.GlAccountIncomePenaltyId is not int incomePenalty)
            {
                return [];
            }

            lines.Add(new GlPostingLine(incomePenalty, Debit: null, Credit: penalty, GlTransactionSubType.RepaymentPenalty));
        }

        if (overpayment > 0)
        {
            if (product.GlAccountLoanOverPaymentsId is not int overpayments)
            {
                return [];
            }

            lines.Add(new GlPostingLine(overpayments, Debit: null, Credit: overpayment, GlTransactionSubType.Overpayment));
        }

        return lines;
    }

    public static IReadOnlyList<GlPostingLine> ForWriteOff(LoanProduct product, decimal principal, decimal interest, decimal fees, decimal penalty)
    {
        var total = principal + interest + fees + penalty;
        if (total <= 0 || product.GlAccountLoansWrittenOffId is not int writtenOff)
        {
            return [];
        }

        var lines = new List<GlPostingLine> { new(writtenOff, Debit: total, Credit: null) };

        if (principal > 0)
        {
            if (product.GlAccountLoanPortfolioId is not int portfolio)
            {
                return [];
            }

            lines.Add(new GlPostingLine(portfolio, Debit: null, Credit: principal, GlTransactionSubType.RepaymentPrincipal));
        }

        if (interest > 0)
        {
            if (product.GlAccountReceivableInterestId is not int receivableInterest)
            {
                return [];
            }

            lines.Add(new GlPostingLine(receivableInterest, Debit: null, Credit: interest, GlTransactionSubType.RepaymentInterest));
        }

        if (fees > 0)
        {
            if (product.GlAccountReceivableFeeId is not int receivableFee)
            {
                return [];
            }

            lines.Add(new GlPostingLine(receivableFee, Debit: null, Credit: fees, GlTransactionSubType.RepaymentFees));
        }

        if (penalty > 0)
        {
            if (product.GlAccountReceivablePenaltyId is not int receivablePenalty)
            {
                return [];
            }

            lines.Add(new GlPostingLine(receivablePenalty, Debit: null, Credit: penalty, GlTransactionSubType.RepaymentPenalty));
        }

        return lines;
    }

    public static IReadOnlyList<GlPostingLine> ForWriteOffRecovery(LoanProduct product, decimal amount)
    {
        if (amount <= 0 || product.GlAccountFundSourceId is not int fundSource || product.GlAccountIncomeRecoveryId is not int incomeRecovery)
        {
            return [];
        }

        return
        [
            new GlPostingLine(fundSource, Debit: amount, Credit: null),
            new GlPostingLine(incomeRecovery, Debit: null, Credit: amount),
        ];
    }

    /// <summary>
    /// FR-LN-20/BR-LN-6's waiver actions, all four components: interest and fee/penalty
    /// waivers reverse an accrued-income/receivable pair; a principal waiver is booked like a
    /// miniature write-off (debit written-off, credit loan portfolio) since forgiving
    /// principal has the same balance-sheet effect as writing it off.
    /// </summary>
    public static IReadOnlyList<GlPostingLine> ForWaiver(LoanProduct product, LoanRepaymentComponent component, decimal amount)
    {
        if (amount <= 0)
        {
            return [];
        }

        (int? DebitAccount, int? CreditAccount) accounts = component switch
        {
            LoanRepaymentComponent.Principal => (product.GlAccountLoansWrittenOffId, product.GlAccountLoanPortfolioId),
            LoanRepaymentComponent.Interest => (product.GlAccountIncomeInterestId, product.GlAccountReceivableInterestId),
            LoanRepaymentComponent.Fees => (product.GlAccountIncomeFeeId, product.GlAccountReceivableFeeId),
            LoanRepaymentComponent.Penalty => (product.GlAccountIncomePenaltyId, product.GlAccountReceivablePenaltyId),
            _ => (null, null),
        };

        if (accounts.DebitAccount is not int debitAccount || accounts.CreditAccount is not int creditAccount)
        {
            return [];
        }

        return
        [
            new GlPostingLine(debitAccount, Debit: amount, Credit: null),
            new GlPostingLine(creditAccount, Debit: null, Credit: amount),
        ];
    }
}
