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

    // Penalty controls — new columns, null on legacy rows. See ChargeRules for which types use which.

    /// <summary>Days after the due date (or maturity) before a late-repayment or default penalty applies.</summary>
    public int? GraceDays { get; set; }

    /// <summary>Null = charged once; otherwise charged again every this many days while still unpaid.</summary>
    public int? RepeatEveryDays { get; set; }

    /// <summary>Ceiling on everything this penalty can add to one loan, as a percentage of the amount disbursed.</summary>
    public decimal? MaxTotalPercent { get; set; }

    /// <summary>Early-closure only: no fee once this many instalments have been paid.</summary>
    public int? FreeAfterInstallments { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
