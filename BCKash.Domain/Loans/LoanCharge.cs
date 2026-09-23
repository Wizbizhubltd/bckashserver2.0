using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_charges` table (BRD §6.5).</summary>
public class LoanCharge : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanId { get; set; }

    /// <summary>References `charges.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ChargeId { get; set; }

    public bool Penalty { get; set; }
    public bool Waived { get; set; }

    public LoanChargeType ChargeType { get; set; }
    public LoanChargeCalculationType ChargeOption { get; set; }

    public decimal? Amount { get; set; }
    public decimal? AmountPaid { get; set; }
    public DateOnly? DueDate { get; set; }
    public int GracePeriod { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
}
