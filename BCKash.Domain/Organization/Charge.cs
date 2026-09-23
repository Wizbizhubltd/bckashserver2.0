using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `charges` table — BRD §6.1.</summary>
public class Charge : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public string? Name { get; set; }
    public int? CurrencyId { get; set; }
    public ChargeProduct Product { get; set; }
    public ChargeType ChargeType { get; set; }
    public ChargeOption ChargeOption { get; set; }
    public int ChargeFrequency { get; set; }
    public ChargeFrequencyType ChargeFrequencyType { get; set; } = Organization.ChargeFrequencyType.Days;
    public int ChargeFrequencyAmount { get; set; }
    public decimal? Amount { get; set; }
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public ChargePaymentMode ChargePaymentMode { get; set; } = Organization.ChargePaymentMode.Regular;
    public bool Active { get; set; } = true;
    public bool Penalty { get; set; }
    public bool Override { get; set; }
    public int? GlAccountIncomeId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
