using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

/// <summary>Mirrors OfficeService's self-referencing-tree pattern (cycle prevention on parent_id).</summary>
public class GlAccountService : IGlAccountService
{
    private readonly BCKashDbContext _db;

    public GlAccountService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<GlAccountWriteResult> CreateAsync(GlAccount account, CancellationToken cancellationToken = default)
    {
        if (account.ParentId.HasValue && !await _db.GlAccounts.AnyAsync(a => a.Id == account.ParentId.Value, cancellationToken))
        {
            return new GlAccountWriteResult(GlAccountWriteOutcome.NotFound);
        }

        _db.GlAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        return new GlAccountWriteResult(GlAccountWriteOutcome.Success, account);
    }

    public async Task<GlAccountWriteResult> UpdateAsync(int id, GlAccount updated, CancellationToken cancellationToken = default)
    {
        var account = await _db.GlAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return new GlAccountWriteResult(GlAccountWriteOutcome.NotFound);
        }

        if (updated.ParentId.HasValue && await CreatesCycleAsync(id, updated.ParentId.Value, cancellationToken))
        {
            return new GlAccountWriteResult(GlAccountWriteOutcome.CircularParent);
        }

        account.Name = updated.Name;
        account.ParentId = updated.ParentId;
        account.GlCode = updated.GlCode;
        account.AccountType = updated.AccountType;
        account.ManualEntries = updated.ManualEntries;
        account.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new GlAccountWriteResult(GlAccountWriteOutcome.Success, account);
    }

    public async Task<GlAccountWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var account = await _db.GlAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return new GlAccountWriteResult(GlAccountWriteOutcome.NotFound);
        }

        account.Active = false;
        await _db.SaveChangesAsync(cancellationToken);
        return new GlAccountWriteResult(GlAccountWriteOutcome.Success, account);
    }

    public async Task<GlAccountWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var account = await _db.GlAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return new GlAccountWriteResult(GlAccountWriteOutcome.NotFound);
        }

        account.Active = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new GlAccountWriteResult(GlAccountWriteOutcome.Success, account);
    }

    private async Task<bool> CreatesCycleAsync(int accountId, int proposedParentId, CancellationToken cancellationToken)
    {
        if (proposedParentId == accountId)
        {
            return true;
        }

        var visited = new HashSet<int>();
        int? current = proposedParentId;

        while (current.HasValue)
        {
            if (current.Value == accountId)
            {
                return true;
            }

            if (!visited.Add(current.Value))
            {
                return false;
            }

            current = await _db.GlAccounts
                .Where(a => a.Id == current.Value)
                .Select(a => a.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}
