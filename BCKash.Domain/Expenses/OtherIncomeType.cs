using BCKash.SharedKernel;

namespace BCKash.Domain.Expenses;

/// <summary>Maps the legacy `other_income_types` table (BRD §6.10).</summary>
public class OtherIncomeType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? GlAccountAssetId { get; set; }
    public int? GlAccountIncomeId { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OtherIncome> OtherIncomes { get; set; } = new List<OtherIncome>();
}
