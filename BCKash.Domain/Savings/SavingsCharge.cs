using BCKash.SharedKernel;

namespace BCKash.Domain.Savings;

/// <summary>Legacy `savings_charges.charge_type`.</summary>
public enum SavingsChargeType
{
    SavingsActivation,
    WithdrawalFee,
    AnnualFee,
    MonthlyFee,
    SpecifiedDueDate
}

/// <summary>Legacy `savings_charges.charge_option`.</summary>
public enum SavingsChargeOption
{
    Flat,
    Percentage
}

/// <summary>
/// Maps the legacy `savings_charges` table (BRD §6.6) — a charge applied to a specific
/// savings account. `charge_id` references the (out-of-scope) `charges` table, so it
/// stays a plain scalar column here.
/// </summary>
public class SavingsCharge : IHasTimestamps
{
    public int Id { get; set; }
    public int? SavingsId { get; set; }
    public int? ChargeId { get; set; }
    public bool Penalty { get; set; }
    public bool Waived { get; set; }
    public SavingsChargeType ChargeType { get; set; }
    public SavingsChargeOption ChargeOption { get; set; }
    public decimal? Amount { get; set; }
    public decimal? AmountPaid { get; set; }
    public DateOnly? DueDate { get; set; }
    public int GracePeriod { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SavingsAccount? Savings { get; set; }
}
