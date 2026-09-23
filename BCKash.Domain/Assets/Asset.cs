using BCKash.SharedKernel;

namespace BCKash.Domain.Assets;

/// <summary>Maps the legacy `assets` table — fixed asset register (BRD §6.9).</summary>
public class Asset : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public int? AssetTypeId { get; set; }
    public int? OfficeId { get; set; }
    public string? Name { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? Value { get; set; }
    public int? LifeSpan { get; set; }
    public decimal? SalvageValue { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
    public string? Files { get; set; }
    public string? PurchaseYear { get; set; }
    public AssetStatus? Status { get; set; }
    public bool? Active { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public AssetType? AssetType { get; set; }
    public ICollection<AssetDepreciation> AssetDepreciations { get; set; } = new List<AssetDepreciation>();
}
