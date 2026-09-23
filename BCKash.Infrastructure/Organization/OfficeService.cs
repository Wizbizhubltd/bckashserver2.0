using BCKash.Application.Organization;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Organization;

public class OfficeService : IOfficeService
{
    // Loan statuses that mean the loan is no longer a live liability for the office —
    // everything else (new/pending/approved/disbursed/need_changes/reschedule states)
    // counts as "open" for the deactivation-in-use check.
    private static readonly HashSet<LoanStatus> TerminalLoanStatuses =
    [
        LoanStatus.Declined,
        LoanStatus.Rejected,
        LoanStatus.Withdrawn,
        LoanStatus.Closed,
        LoanStatus.Paid,
        LoanStatus.WrittenOff,
    ];

    private readonly BCKashDbContext _db;

    public OfficeService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<OfficeWriteResult> CreateAsync(Office office, CancellationToken cancellationToken = default)
    {
        _db.Offices.Add(office);
        await _db.SaveChangesAsync(cancellationToken);
        return new OfficeWriteResult(OfficeWriteOutcome.Success, office);
    }

    public async Task<OfficeWriteResult> UpdateAsync(int id, Office updated, CancellationToken cancellationToken = default)
    {
        var office = await _db.Offices.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (office is null)
        {
            return new OfficeWriteResult(OfficeWriteOutcome.NotFound);
        }

        if (updated.ParentId.HasValue && await CreatesCycleAsync(id, updated.ParentId.Value, cancellationToken))
        {
            return new OfficeWriteResult(OfficeWriteOutcome.CircularParent);
        }

        office.Name = updated.Name;
        office.ParentId = updated.ParentId;
        office.ExternalId = updated.ExternalId;
        office.OpeningDate = updated.OpeningDate;
        office.Address = updated.Address;
        office.Phone = updated.Phone;
        office.Email = updated.Email;
        office.Notes = updated.Notes;
        office.ManagerId = updated.ManagerId;
        office.DefaultOffice = updated.DefaultOffice;

        await _db.SaveChangesAsync(cancellationToken);
        return new OfficeWriteResult(OfficeWriteOutcome.Success, office);
    }

    public async Task<OfficeWriteResult> DeactivateAsync(int id, bool confirm, CancellationToken cancellationToken = default)
    {
        var office = await _db.Offices.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (office is null)
        {
            return new OfficeWriteResult(OfficeWriteOutcome.NotFound);
        }

        var activeClientCount = await _db.Clients.CountAsync(c => c.OfficeId == id && c.Status == ClientStatus.Active, cancellationToken);
        var openLoanCount = await _db.Loans.CountAsync(l => l.OfficeId == id && !TerminalLoanStatuses.Contains(l.Status), cancellationToken);

        if (!confirm && (activeClientCount > 0 || openLoanCount > 0))
        {
            return new OfficeWriteResult(
                OfficeWriteOutcome.InUseConfirmationRequired,
                office,
                new OfficeInUseCounts(activeClientCount, openLoanCount));
        }

        office.Active = false;
        await _db.SaveChangesAsync(cancellationToken);
        return new OfficeWriteResult(OfficeWriteOutcome.Success, office);
    }

    public async Task<OfficeWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var office = await _db.Offices.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (office is null)
        {
            return new OfficeWriteResult(OfficeWriteOutcome.NotFound);
        }

        office.Active = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new OfficeWriteResult(OfficeWriteOutcome.Success, office);
    }

    /// <summary>True if walking <paramref name="proposedParentId"/>'s ancestor chain reaches <paramref name="officeId"/>.</summary>
    private async Task<bool> CreatesCycleAsync(int officeId, int proposedParentId, CancellationToken cancellationToken)
    {
        if (proposedParentId == officeId)
        {
            return true;
        }

        var visited = new HashSet<int>();
        int? current = proposedParentId;

        while (current.HasValue)
        {
            if (current.Value == officeId)
            {
                return true;
            }

            // Guards against a pre-existing bad chain (e.g. corrupt data) looping forever.
            if (!visited.Add(current.Value))
            {
                return false;
            }

            current = await _db.Offices
                .Where(o => o.Id == current.Value)
                .Select(o => o.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}
