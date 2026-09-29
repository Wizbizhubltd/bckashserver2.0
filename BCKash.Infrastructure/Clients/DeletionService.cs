using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IDeletionService"/>.</summary>
public class DeletionService : IDeletionService
{
    // A loan in any of these still has money or a decision outstanding.
    // A List, not an array: array.Contains in a query binds to the span overload, which EF can't translate.
    private static readonly List<LoanStatus> OpenLoanStatuses =
    [
        LoanStatus.New, LoanStatus.Pending, LoanStatus.Approved, LoanStatus.NeedChanges,
        LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled,
    ];

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public DeletionService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<bool> GroupHasApprovedMemberAsync(int groupId, CancellationToken cancellationToken = default) =>
        _db.GroupClients
            .Where(gc => gc.GroupId == groupId && gc.RemovedAt == null)
            .AnyAsync(gc => gc.Client!.ActivatedDate != null || gc.Client.Status == ClientStatus.Active, cancellationToken);

    public async Task<DeletionResult> DeleteClientAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken);
        if (client is null)
        {
            return new DeletionResult(DeletionOutcome.NotFound);
        }

        if (!ClientDeletionRules.CanDeleteDirectly(client))
        {
            return new DeletionResult(DeletionOutcome.RequiresRequest);
        }

        return await RemoveClientAsync(client, cancellationToken);
    }

    public async Task<DeletionResult> DeleteGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return new DeletionResult(DeletionOutcome.NotFound);
        }

        if (group.ActivatedDate.HasValue || await GroupHasApprovedMemberAsync(groupId, cancellationToken))
        {
            return new DeletionResult(DeletionOutcome.RequiresRequest);
        }

        return await RemoveGroupAsync(groupId, cancellationToken);
    }

    public async Task<DeletionResult> RequestAsync(string entityType, int entityId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new DeletionResult(DeletionOutcome.ReasonRequired);
        }

        if (await _db.DeletionRequests.AnyAsync(r => r.EntityType == entityType && r.EntityId == entityId && r.Status == DeletionRequestStatus.Pending, cancellationToken))
        {
            return new DeletionResult(DeletionOutcome.AlreadyRequested);
        }

        string? name;
        int? officeId;
        if (entityType == DeletionRequest.ClientEntity)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == entityId && c.DeletedAt == null, cancellationToken);
            if (client is null)
            {
                return new DeletionResult(DeletionOutcome.NotFound);
            }

            (name, officeId) = (client.DisplayName ?? $"{client.FirstName} {client.LastName}".Trim(), client.OfficeId);
        }
        else
        {
            var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == entityId, cancellationToken);
            if (group is null)
            {
                return new DeletionResult(DeletionOutcome.NotFound);
            }

            (name, officeId) = (group.Name, group.OfficeId);
        }

        var request = new DeletionRequest
        {
            EntityType = entityType,
            EntityId = entityId,
            EntityName = name,
            OfficeId = officeId,
            Reason = reason.Trim(),
            RequestedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.DeletionRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        return new DeletionResult(DeletionOutcome.Success, request);
    }

    public async Task<DeletionResult> ReviewAsync(int requestId, bool approve, string? note, CancellationToken cancellationToken = default)
    {
        var request = await _db.DeletionRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return new DeletionResult(DeletionOutcome.NotFound);
        }

        if (request.Status != DeletionRequestStatus.Pending)
        {
            return new DeletionResult(DeletionOutcome.AlreadyReviewed, request);
        }

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            return new DeletionResult(DeletionOutcome.ReasonRequired, request);
        }

        if (approve)
        {
            DeletionResult removed;
            if (request.EntityType == DeletionRequest.ClientEntity)
            {
                var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.EntityId && c.DeletedAt == null, cancellationToken);
                removed = client is null ? new DeletionResult(DeletionOutcome.Success) : await RemoveClientAsync(client, cancellationToken, save: false);
            }
            else
            {
                removed = await RemoveGroupAsync(request.EntityId, cancellationToken, save: false);
            }

            if (removed.Outcome is not (DeletionOutcome.Success or DeletionOutcome.NotFound))
            {
                return removed with { Request = request };
            }
        }

        request.Status = approve ? DeletionRequestStatus.Approved : DeletionRequestStatus.Rejected;
        request.ReviewedById = _currentUser.UserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new DeletionResult(DeletionOutcome.Success, request);
    }

    /// <summary>Soft-deletes the client and takes them out of their groups.</summary>
    private async Task<DeletionResult> RemoveClientAsync(Domain.Clients.Client client, CancellationToken cancellationToken, bool save = true)
    {
        var hasOpenLoans = await _db.Loans.AnyAsync(l => l.ClientId == client.Id && OpenLoanStatuses.Contains(l.Status), cancellationToken)
            || await _db.LoanApplications.AnyAsync(a => a.ClientId == client.Id && a.Status == ApprovalStatus.Pending, cancellationToken);
        if (hasOpenLoans)
        {
            return new DeletionResult(DeletionOutcome.HasOpenLoans);
        }

        client.DeletedAt = DateTime.UtcNow;
        foreach (var membership in await _db.GroupClients.Where(gc => gc.ClientId == client.Id && gc.RemovedAt == null).ToListAsync(cancellationToken))
        {
            membership.RemovedAt = DateTime.UtcNow;
            membership.RemovedById = _currentUser.UserId;
        }

        if (save)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new DeletionResult(DeletionOutcome.Success);
    }

    /// <summary>Removes the group and its memberships. Its clients stay on the platform as individual clients.</summary>
    private async Task<DeletionResult> RemoveGroupAsync(int groupId, CancellationToken cancellationToken, bool save = true)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return new DeletionResult(DeletionOutcome.NotFound);
        }

        var hasLoans = await _db.Loans.AnyAsync(l => l.GroupId == groupId, cancellationToken)
            || await _db.LoanApplications.AnyAsync(a => a.GroupId == groupId, cancellationToken);
        if (hasLoans)
        {
            return new DeletionResult(DeletionOutcome.HasOpenLoans);
        }

        _db.GroupClients.RemoveRange(await _db.GroupClients.Where(gc => gc.GroupId == groupId).ToListAsync(cancellationToken));
        _db.GroupUsers.RemoveRange(await _db.GroupUsers.Where(gu => gu.GroupId == groupId).ToListAsync(cancellationToken));
        _db.Groups.Remove(group);

        if (save)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new DeletionResult(DeletionOutcome.Success);
    }
}
