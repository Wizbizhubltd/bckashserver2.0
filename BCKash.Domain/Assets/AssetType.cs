using BCKash.SharedKernel;

namespace BCKash.Domain.Assets;

/// <summary>Maps the legacy `asset_types` table (BRD §6.9).</summary>
public class AssetType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? GlAccountFixedAssetId { get; set; }
    public int? GlAccountAssetId { get; set; }
    public int? GlAccountContraAssetId { get; set; }
    public int? GlAccountExpenseId { get; set; }
    public int? GlAccountLiabilityId { get; set; }
    public int? GlAccountIncomeId { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
