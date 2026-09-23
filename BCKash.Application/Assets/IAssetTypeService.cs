using BCKash.Domain.Assets;

namespace BCKash.Application.Assets;

public enum AssetTypeWriteOutcome
{
    Success,
    NotFound,

    /// <summary>Delete blocked — an Asset already references this type (mirrors FR-LN-1's "deactivate instead" precedent; asset types have no active flag, so delete is simply blocked).</summary>
    InUse,
}

public record AssetTypeWriteResult(AssetTypeWriteOutcome Outcome, AssetType? AssetType = null);

/// <summary>Fixed-asset type ("product") CRUD — the GL-account configuration used by depreciation (BR-AST-1). Mirrors ILoanProductService's shape.</summary>
public interface IAssetTypeService
{
    Task<AssetTypeWriteResult> CreateAsync(AssetType assetType, CancellationToken cancellationToken = default);

    Task<AssetTypeWriteResult> UpdateAsync(int id, AssetType updated, CancellationToken cancellationToken = default);

    Task<AssetTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
