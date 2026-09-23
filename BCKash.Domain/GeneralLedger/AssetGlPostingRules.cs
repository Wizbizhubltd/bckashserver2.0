using BCKash.Domain.Assets;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Pure double-entry posting rules for fixed-asset depreciation (BR-AST-2, FR-AST-2). Debits
/// the asset type's configured depreciation-expense account and credits its contra-asset
/// (accumulated depreciation) account — standard MFI/GAAP double-entry depreciation, not
/// extracted from or validated against the legacy PHP codebase (same caveat as
/// docs/gl-posting-spec.md). Returns an empty list — posting skipped, not partial — when the
/// asset type isn't fully configured for GL.
/// </summary>
public static class AssetGlPostingRules
{
    public static IReadOnlyList<GlPostingLine> ForDepreciation(AssetType assetType, decimal amount)
    {
        if (amount <= 0 || assetType.GlAccountExpenseId is not int expense || assetType.GlAccountContraAssetId is not int contraAsset)
        {
            return [];
        }

        return
        [
            new GlPostingLine(expense, Debit: amount, Credit: null),
            new GlPostingLine(contraAsset, Debit: null, Credit: amount),
        ];
    }
}
