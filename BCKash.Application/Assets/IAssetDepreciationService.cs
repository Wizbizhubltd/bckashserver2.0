using BCKash.Domain.Assets;

namespace BCKash.Application.Assets;

public enum AssetDepreciationOutcome
{
    Success,
    NotFound,

    /// <summary>Only active assets are depreciated (BR-AST-2).</summary>
    NotActive,

    /// <summary>A depreciation row already exists for this asset and year — the job is idempotent per (asset, year).</summary>
    AlreadyRun,

    /// <summary>The asset has already reached its salvage value; nothing left to depreciate.</summary>
    FullyDepreciated,

    /// <summary>Cost, salvage value or useful life aren't set (or cost isn't greater than salvage value) — can't compute a schedule.</summary>
    MissingConfiguration,
}

public record AssetDepreciationResult(AssetDepreciationOutcome Outcome, AssetDepreciation? Depreciation = null);

/// <summary>
/// The annual depreciation job (BR-AST-2, FR-AST-2) — confirmed straight-line (see
/// <see cref="BCKash.Domain.Assets.AssetDepreciationRules"/>'s doc comment). No scheduler exists
/// anywhere in this codebase (same situation as FR-SAV-4/FR-LN-25), so this is exposed as an
/// admin-triggered endpoint rather than a background job.
/// </summary>
public interface IAssetDepreciationService
{
    /// <summary>Runs the next due depreciation year for one asset, tagged with the given calendar year.</summary>
    Task<AssetDepreciationResult> RunAsync(int assetId, string year, CancellationToken cancellationToken = default);

    /// <summary>Runs every active asset that doesn't yet have a depreciation row for the given year. Returns how many assets were successfully depreciated.</summary>
    Task<int> RunDueAsync(string year, CancellationToken cancellationToken = default);
}
