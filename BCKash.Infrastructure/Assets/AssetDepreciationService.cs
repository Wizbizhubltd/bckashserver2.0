using BCKash.Application.Assets;
using BCKash.Domain.Assets;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Assets;

public class AssetDepreciationService : IAssetDepreciationService
{
    private readonly BCKashDbContext _db;
    private readonly IAssetGlPostingService _glPostingService;

    public AssetDepreciationService(BCKashDbContext db, IAssetGlPostingService glPostingService)
    {
        _db = db;
        _glPostingService = glPostingService;
    }

    public async Task<AssetDepreciationResult> RunAsync(int assetId, string year, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return new AssetDepreciationResult(AssetDepreciationOutcome.NotFound);
        }

        if (asset.Status != AssetStatus.Active)
        {
            return new AssetDepreciationResult(AssetDepreciationOutcome.NotActive);
        }

        if (await _db.AssetDepreciations.AnyAsync(d => d.AssetId == assetId && d.Year == year, cancellationToken))
        {
            return new AssetDepreciationResult(AssetDepreciationOutcome.AlreadyRun);
        }

        var cost = asset.PurchasePrice ?? 0m;
        var salvage = asset.SalvageValue ?? 0m;
        var lifeSpan = asset.LifeSpan ?? 0;
        if (lifeSpan <= 0 || cost <= salvage)
        {
            return new AssetDepreciationResult(AssetDepreciationOutcome.MissingConfiguration);
        }

        var existingRows = await _db.AssetDepreciations
            .Where(d => d.AssetId == assetId)
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var accumulatedSoFar = existingRows.Sum(d => d.DepreciationValue ?? 0m);
        var nextYearNumber = existingRows.Count + 1;

        var computed = AssetDepreciationRules.ComputeNextYear(cost, salvage, lifeSpan, accumulatedSoFar, nextYearNumber);
        if (computed is null)
        {
            return new AssetDepreciationResult(AssetDepreciationOutcome.FullyDepreciated);
        }

        var depreciation = new AssetDepreciation
        {
            AssetId = assetId,
            Year = year,
            BeginningValue = computed.BeginningValue,
            DepreciationValue = computed.DepreciationValue,
            Rate = computed.Rate,
            Cost = cost,
            Accumulated = computed.Accumulated,
            EndingValue = computed.EndingValue,
        };

        _db.AssetDepreciations.Add(depreciation);
        asset.Value = computed.EndingValue;
        await _db.SaveChangesAsync(cancellationToken);

        await _glPostingService.PostDepreciationAsync(asset, depreciation, cancellationToken);

        return new AssetDepreciationResult(AssetDepreciationOutcome.Success, depreciation);
    }

    public async Task<int> RunDueAsync(string year, CancellationToken cancellationToken = default)
    {
        var candidateIds = await _db.Assets
            .Where(a => a.Status == AssetStatus.Active)
            .Where(a => !_db.AssetDepreciations.Any(d => d.AssetId == a.Id && d.Year == year))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var succeeded = 0;
        foreach (var assetId in candidateIds)
        {
            var result = await RunAsync(assetId, year, cancellationToken);
            if (result.Outcome == AssetDepreciationOutcome.Success)
            {
                succeeded++;
            }
        }

        return succeeded;
    }
}
