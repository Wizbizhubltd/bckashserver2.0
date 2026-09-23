using BCKash.Application.Assets;
using BCKash.Domain.Assets;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Assets;

public class AssetService : IAssetService
{
    private readonly BCKashDbContext _db;

    public AssetService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<AssetWriteResult> CreateAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        if (asset.AssetTypeId is int typeId && !await _db.AssetTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new AssetWriteResult(AssetWriteOutcome.TypeNotFound);
        }

        asset.Status ??= AssetStatus.Active;
        asset.Value ??= asset.PurchasePrice;

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return new AssetWriteResult(AssetWriteOutcome.Success, asset);
    }

    public async Task<AssetWriteResult> UpdateAsync(int id, Asset updated, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return new AssetWriteResult(AssetWriteOutcome.NotFound);
        }

        if (updated.AssetTypeId is int typeId && !await _db.AssetTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new AssetWriteResult(AssetWriteOutcome.TypeNotFound);
        }

        asset.AssetTypeId = updated.AssetTypeId;
        asset.OfficeId = updated.OfficeId;
        asset.Name = updated.Name;
        asset.PurchaseDate = updated.PurchaseDate;
        asset.PurchasePrice = updated.PurchasePrice;
        asset.LifeSpan = updated.LifeSpan;
        asset.SalvageValue = updated.SalvageValue;
        asset.SerialNumber = updated.SerialNumber;
        asset.Notes = updated.Notes;
        asset.Files = updated.Files;
        asset.PurchaseYear = updated.PurchaseYear;

        await _db.SaveChangesAsync(cancellationToken);
        return new AssetWriteResult(AssetWriteOutcome.Success, asset);
    }

    public async Task<AssetWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return new AssetWriteResult(AssetWriteOutcome.NotFound);
        }

        var hasDepreciation = await _db.AssetDepreciations.AnyAsync(d => d.AssetId == id, cancellationToken);
        if (hasDepreciation)
        {
            return new AssetWriteResult(AssetWriteOutcome.InUse, asset);
        }

        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return new AssetWriteResult(AssetWriteOutcome.Success);
    }

    /// <summary>Sold/WrittenOff are terminal — once an asset reaches either, no further status changes are allowed.</summary>
    public async Task<AssetWriteResult> ChangeStatusAsync(int id, AssetStatus newStatus, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return new AssetWriteResult(AssetWriteOutcome.NotFound);
        }

        if (asset.Status is AssetStatus.Sold or AssetStatus.WrittenOff)
        {
            return new AssetWriteResult(AssetWriteOutcome.InvalidTransition, asset);
        }

        asset.Status = newStatus;
        await _db.SaveChangesAsync(cancellationToken);
        return new AssetWriteResult(AssetWriteOutcome.Success, asset);
    }
}
