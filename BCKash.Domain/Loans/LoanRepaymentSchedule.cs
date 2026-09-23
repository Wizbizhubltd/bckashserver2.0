using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_repayment_schedules` table (BRD §6.5).</summary>
public class LoanRepaymentSchedule : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanId { get; set; }

    /// <summary>Installment sequence number (1st, 2nd, ... repayment).</summary>
    public int? Installment { get; set; }

    public DateOnly? DueDate { get; set; }
    public DateOnly? FromDate { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }

    public decimal? Principal { get; set; }
    public decimal? PrincipalWaived { get; set; }
    public decimal? PrincipalWrittenOff { get; set; }
    public decimal? PrincipalPaid { get; set; }

    public decimal? Interest { get; set; }
    public decimal? InterestWaived { get; set; }
    public decimal? InterestWrittenOff { get; set; }
    public decimal? InterestPaid { get; set; }

    public decimal? Fees { get; set; }
    public decimal? FeesWaived { get; set; }
    public decimal? FeesWrittenOff { get; set; }
    public decimal? FeesPaid { get; set; }

    public decimal? Penalty { get; set; }
    public decimal? PenaltyWaived { get; set; }
    public decimal? PenaltyWrittenOff { get; set; }
    public decimal? PenaltyPaid { get; set; }

    public decimal? TotalDue { get; set; }
    public decimal? TotalPaidAdvance { get; set; }
    public decimal? TotalPaidLate { get; set; }

    public bool Paid { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ModifiedById { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CreatedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
    public ICollection<LoanTransaction> LoanTransactions { get; set; } = new List<LoanTransaction>();
    public ICollection<LoanTransactionRepaymentScheduleMapping> LoanTransactionRepaymentScheduleMappings { get; set; } = new List<LoanTransactionRepaymentScheduleMapping>();
}
