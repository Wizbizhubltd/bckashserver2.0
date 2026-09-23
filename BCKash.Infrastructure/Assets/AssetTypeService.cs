using BCKash.Application.Assets;
using BCKash.Domain.Assets;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Assets;

public class AssetTypeService : IAssetTypeService
{
    private readonly BCKashDbContext _db;

    public AssetTypeService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<AssetTypeWriteResult> CreateAsync(AssetType assetType, CancellationToken cancellationToken = default)
    {
        _db.AssetTypes.Add(assetType);
        await _db.SaveChangesAsync(cancellationToken);
        return new AssetTypeWriteResult(AssetTypeWriteOutcome.Success, assetType);
    }

    public async Task<AssetTypeWriteResult> UpdateAsync(int id, AssetType updated, CancellationToken cancellationToken = default)
    {
        var assetType = await _db.AssetTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (assetType is null)
        {
            return new AssetTypeWriteResult(AssetTypeWriteOutcome.NotFound);
        }

        assetType.Name = updated.Name;
        assetType.GlAccountFixedAssetId = updated.GlAccountFixedAssetId;
        assetType.GlAccountAssetId = updated.GlAccountAssetId;
        assetType.GlAccountContraAssetId = updated.GlAccountContraAssetId;
        assetType.GlAccountExpenseId = updated.GlAccountExpenseId;
        assetType.GlAccountLiabilityId = updated.GlAccountLiabilityId;
        assetType.GlAccountIncomeId = updated.GlAccountIncomeId;
        assetType.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new AssetTypeWriteResult(AssetTypeWriteOutcome.Success, assetType);
    }

    public async Task<AssetTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var assetType = await _db.AssetTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (assetType is null)
        {
            return new AssetTypeWriteResult(AssetTypeWriteOutcome.NotFound);
        }

        var inUse = await _db.Assets.AnyAsync(a => a.AssetTypeId == id, cancellationToken);
        if (inUse)
        {
            return new AssetTypeWriteResult(AssetTypeWriteOutcome.InUse, assetType);
        }

        _db.AssetTypes.Remove(assetType);
        await _db.SaveChangesAsync(cancellationToken);
        return new AssetTypeWriteResult(AssetTypeWriteOutcome.Success);
    }
}
