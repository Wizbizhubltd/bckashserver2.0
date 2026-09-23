using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_transactions` table (BRD §6.5, FR-LN-17).</summary>
public class LoanTransaction : IHasTimestamps, ISoftDelete, IAuditable
{
    public int Id { get; set; }

    public int? LoanId { get; set; }

    /// <summary>References `offices.id` — outside this table group, kept as a plain scalar.</summary>
    public int? OfficeId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    /// <summary>References `payment_types.id` — outside this table group, kept as a plain scalar.</summary>
    public int? PaymentTypeId { get; set; }

    public LoanTransactionType? TransactionType { get; set; } = LoanTransactionType.Repayment;

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CreatedById { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ModifiedById { get; set; }

    /// <summary>References `payment_details.id` — outside this table group, kept as a plain scalar.</summary>
    public int? PaymentDetailId { get; set; }

    /// <summary>References `charges.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ChargeId { get; set; }

    public int? LoanRepaymentScheduleId { get; set; }

    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public decimal? Balance { get; set; }
    public decimal? Amount { get; set; }

    public bool Reversible { get; set; }
    public bool Reversed { get; set; }
    public LoanTransactionReversalType ReversalType { get; set; } = LoanTransactionReversalType.None;
    public LoanPaymentApplyTo? PaymentApplyTo { get; set; } = LoanPaymentApplyTo.Regular;

    public ApprovalStatus? Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ApprovedById { get; set; }

    public DateOnly? ApprovedDate { get; set; }

    public decimal? Interest { get; set; }
    public decimal? Principal { get; set; }
    public decimal? Fee { get; set; }
    public decimal? Penalty { get; set; }
    public decimal? Overpayment { get; set; }

    public DateOnly? Date { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }
    public string? Receipt { get; set; }

    public decimal? PrincipalDerived { get; set; }
    public decimal? InterestDerived { get; set; }
    public decimal? FeesDerived { get; set; }
    public decimal? PenaltyDerived { get; set; }
    public decimal? OverpaymentDerived { get; set; }
    public decimal? UnrecognizedIncomeDerived { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Loan? Loan { get; set; }
    public LoanRepaymentSchedule? LoanRepaymentSchedule { get; set; }
    public ICollection<LoanTransactionRepaymentScheduleMapping> LoanTransactionRepaymentScheduleMappings { get; set; } = new List<LoanTransactionRepaymentScheduleMapping>();
}
