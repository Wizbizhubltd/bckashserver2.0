using BCKash.Domain.Savings;
using BCKash.SharedKernel;

namespace BCKash.Domain.GeneralLedger;

/// <summary>Legacy `gl_journal_entries.transaction_type`.</summary>
public enum GlTransactionType
{
    Disbursement,
    Accrual,
    Deposit,
    Withdrawal,
    ManualEntry,
    PayCharge,
    TransferFund,
    Expense,
    Payroll,
    Income,
    Fee,
    Penalty,
    Interest,
    Dividend,
    Guarantee,
    WriteOff,
    Repayment,
    RepaymentDisbursement,
    RepaymentRecovery,
    InterestAccrual,
    FeeAccrual,
    Savings,
    Shares,
    Asset,
    AssetIncome,
    AssetExpense,
    AssetDepreciation
}

/// <summary>Legacy `gl_journal_entries.transaction_sub_type`.</summary>
public enum GlTransactionSubType
{
    Overpayment,
    RepaymentInterest,
    RepaymentPrincipal,
    RepaymentFees,
    RepaymentPenalty
}

/// <summary>
/// Maps the legacy `gl_journal_entries` table (BRD §6.7) — one GL posting line. Only
/// `gl_account_id`, `savings_id` and `gl_closure_id` get navigations, since they
/// reference tables inside this table group; `loan_id`, `loan_transaction_id` and
/// `savings_transaction_id` are cross-module traceability columns pointing at loan
/// tables outside this table group and are kept as plain scalar columns, along with
/// the other out-of-scope FKs (office_id, currency_id, shares_transaction_id,
/// payroll_transaction_id, payment_detail_id, transaction_id, created_by_id,
/// modified_by_id, approved_by_id). No GL-posting business logic is implemented here.
/// </summary>
public class GlJournalEntry : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? OfficeId { get; set; }
    public int? GlAccountId { get; set; }
    public int? CurrencyId { get; set; }
    public GlTransactionType? TransactionType { get; set; } = GlTransactionType.Repayment;
    public GlTransactionSubType? TransactionSubType { get; set; }
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public bool Reversed { get; set; }
    public string? Name { get; set; }
    public string? Reference { get; set; }
    public int? LoanId { get; set; }
    public int? LoanTransactionId { get; set; }
    public int? SavingsTransactionId { get; set; }
    public int? SavingsId { get; set; }
    public int? SharesTransactionId { get; set; }
    public int? PayrollTransactionId { get; set; }
    public int? PaymentDetailId { get; set; }
    public int? TransactionId { get; set; }
    public int? GlClosureId { get; set; }
    public DateOnly? Date { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }
    public string? Notes { get; set; }
    public string? Narration { get; set; }
    public int? CreatedById { get; set; }
    public int? ModifiedById { get; set; }
    public bool Reconciled { get; set; }
    public bool ManualEntry { get; set; }
    public bool Approved { get; set; } = true;
    public int? ApprovedById { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public string? ApprovedNotes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public GlAccount? GlAccount { get; set; }
    public SavingsAccount? Savings { get; set; }
    public GlClosure? GlClosure { get; set; }
}
