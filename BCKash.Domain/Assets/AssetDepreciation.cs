using BCKash.SharedKernel;

namespace BCKash.Domain.Assets;

/// <summary>Maps the legacy `asset_depreciation` table — yearly depreciation schedule rows (BRD §6.9).</summary>
public class AssetDepreciation : IHasTimestamps
{
    public int Id { get; set; }
    public int? AssetId { get; set; }
    public string? Year { get; set; }
    public decimal? BeginningValue { get; set; }
    public decimal? DepreciationValue { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Accumulated { get; set; }
    public decimal? EndingValue { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Asset? Asset { get; set; }
}
