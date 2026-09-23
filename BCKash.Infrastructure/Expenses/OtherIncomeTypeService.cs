using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class OtherIncomeTypeService : IOtherIncomeTypeService
{
    private readonly BCKashDbContext _db;

    public OtherIncomeTypeService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<OtherIncomeTypeWriteResult> CreateAsync(OtherIncomeType incomeType, CancellationToken cancellationToken = default)
    {
        _db.OtherIncomeTypes.Add(incomeType);
        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.Success, incomeType);
    }

    public async Task<OtherIncomeTypeWriteResult> UpdateAsync(int id, OtherIncomeType updated, CancellationToken cancellationToken = default)
    {
        var incomeType = await _db.OtherIncomeTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (incomeType is null)
        {
            return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.NotFound);
        }

        incomeType.Name = updated.Name;
        incomeType.GlAccountAssetId = updated.GlAccountAssetId;
        incomeType.GlAccountIncomeId = updated.GlAccountIncomeId;
        incomeType.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.Success, incomeType);
    }

    public async Task<OtherIncomeTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var incomeType = await _db.OtherIncomeTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (incomeType is null)
        {
            return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.NotFound);
        }

        var inUse = await _db.OtherIncomes.AnyAsync(o => o.OtherIncomeTypeId == id, cancellationToken);
        if (inUse)
        {
            return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.InUse, incomeType);
        }

        _db.OtherIncomeTypes.Remove(incomeType);
        await _db.SaveChangesAsync(cancellationToken);
        return new OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome.Success);
    }
}
