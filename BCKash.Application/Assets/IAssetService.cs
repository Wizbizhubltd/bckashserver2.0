using BCKash.Domain.Assets;

namespace BCKash.Application.Assets;

public enum AssetWriteOutcome
{
    Success,
    NotFound,
    TypeNotFound,

    /// <summary>The requested status change isn't valid from the asset's current status (e.g. a sold asset can't be reactivated).</summary>
    InvalidTransition,

    /// <summary>Delete blocked — the asset already has depreciation history.</summary>
    InUse,
}

public record AssetWriteResult(AssetWriteOutcome Outcome, Asset? Asset = null);

/// <summary>Fixed-asset register CRUD plus its status lifecycle (BR-AST-1: active/inactive/sold/damaged/written off).</summary>
public interface IAssetService
{
    Task<AssetWriteResult> CreateAsync(Asset asset, CancellationToken cancellationToken = default);

    Task<AssetWriteResult> UpdateAsync(int id, Asset updated, CancellationToken cancellationToken = default);

    Task<AssetWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<AssetWriteResult> ChangeStatusAsync(int id, AssetStatus newStatus, CancellationToken cancellationToken = default);
}
