using BCKash.Domain.Assets;

namespace BCKash.Application.Assets;

/// <summary>
/// GL posting hook for fixed-asset depreciation (BR-AST-2, FR-AST-2). Loads the asset's type,
/// builds balanced posting lines via <see cref="BCKash.Domain.GeneralLedger.AssetGlPostingRules"/>,
/// and — unless the depreciation date falls on/before an active GL closure for the asset's
/// office, or the type isn't fully configured for GL — writes one
/// <see cref="BCKash.Domain.GeneralLedger.GlJournalEntry"/> row per line, all sharing one
/// `Reference` so they can be looked up/reversed together.
/// </summary>
public interface IAssetGlPostingService
{
    Task PostDepreciationAsync(Asset asset, AssetDepreciation depreciation, CancellationToken cancellationToken = default);
}
