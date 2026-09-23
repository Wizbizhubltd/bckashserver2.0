using BCKash.SharedKernel;

namespace BCKash.Domain.Savings;

/// <summary>Legacy `savings_transactions.transaction_type`.</summary>
public enum SavingsTransactionType
{
    Deposit,
    Withdrawal,
    BankFees,
    Interest,
    Dividend,
    Guarantee,
    GuaranteeRestored,
    FeesPayment,
    TransferLoan,
    TransferSavings,
    SpecifiedDueDateFee
}

/// <summary>Legacy `savings_transactions.reversal_type`.</summary>
public enum SavingsTransactionReversalType
{
    System,
    User,
    None
}

/// <summary>Legacy `savings_transactions.status`.</summary>
public enum SavingsTransactionStatus
{
    Pending,
    Approved,
    Declined
}

/// <summary>
/// Maps the legacy `savings_transactions` table (BRD §6.6). FKs to tables outside this
/// table group (office_id, payment_detail_id, created_by_id, modified_by_id,
/// approved_by_id) are kept as plain scalar columns — no navigation, per the Phase 0
/// data-model baseline. No GL-posting logic is implemented here.
/// </summary>
public class SavingsTransaction : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public int? OfficeId { get; set; }
    public int? ModifiedById { get; set; }
    public int? PaymentDetailId { get; set; }
    public int? SavingsId { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public decimal? Balance { get; set; }
    public SavingsTransactionType? TransactionType { get; set; }
    public bool Reversible { get; set; }
    public bool Reversed { get; set; }
    public SavingsTransactionReversalType ReversalType { get; set; } = SavingsTransactionReversalType.None;
    public SavingsTransactionStatus? Status { get; set; } = SavingsTransactionStatus.Pending;
    public int? ApprovedById { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public bool SystemInterest { get; set; }
    public DateOnly? Date { get; set; }
    public string? Time { get; set; }
    public string? Year { get; set; }
    public string? Month { get; set; }
    public string? Notes { get; set; }
    public DateOnly? BalanceDate { get; set; }
    public int? BalanceDays { get; set; }
    public int? CumulativeBalanceDays { get; set; }
    public decimal? CumulativeBalance { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SavingsAccount? Savings { get; set; }
}
