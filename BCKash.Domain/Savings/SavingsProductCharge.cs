using BCKash.SharedKernel;

namespace BCKash.Domain.Savings;

/// <summary>
/// Maps the legacy `savings_product_charges` table (BRD §6.6) — a charge template
/// attached to a savings product. `charge_id` references the (out-of-scope) `charges`
/// table, so it stays a plain scalar column here.
/// </summary>
public class SavingsProductCharge : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public int? ChargeId { get; set; }
    public int? SavingsProductId { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? Date { get; set; }
    public int GracePeriod { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SavingsProduct? SavingsProduct { get; set; }
}
