using BCKash.Application.Assets;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.Assets;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Assets;

public class AssetGlPostingService : IAssetGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public AssetGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostDepreciationAsync(Asset asset, AssetDepreciation depreciation, CancellationToken cancellationToken = default)
    {
        var assetType = await _db.AssetTypes.FirstOrDefaultAsync(t => t.Id == asset.AssetTypeId, cancellationToken);
        if (assetType is null)
        {
            return;
        }

        var lines = AssetGlPostingRules.ForDepreciation(assetType, depreciation.DepreciationValue ?? 0m);
        if (lines.Count == 0)
        {
            return;
        }

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(asset.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"ASSET-DEPRECIATION-{depreciation.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = asset.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = GlTransactionType.AssetDepreciation,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                TransactionId = depreciation.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
