using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_product_charges` table (BRD §6.5).</summary>
public class LoanProductCharge : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanProductId { get; set; }

    /// <summary>References `charges.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ChargeId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public LoanProduct? LoanProduct { get; set; }
}
