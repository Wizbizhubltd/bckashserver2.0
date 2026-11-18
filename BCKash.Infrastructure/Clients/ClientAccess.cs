using BCKash.Application.Clients;
using BCKash.Application.Files;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IClientAccess"/>.</summary>
public class ClientAccess : IClientAccess
{
    private readonly BCKashDbContext _db;
    private readonly IOfficeScope _scope;
    private readonly ICurrentUserContext _currentUser;
    private readonly IFileStorageService _files;

    public ClientAccess(BCKashDbContext db, IOfficeScope scope, ICurrentUserContext currentUser, IFileStorageService files)
    {
        _db = db;
        _scope = scope;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task<Client?> FindVisibleAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken);
        return client is not null && await _scope.CanAccessOfficeAsync(client.OfficeId, cancellationToken) ? client : null;
    }

    public async Task<bool> CanDocumentAsync(Client client, CancellationToken cancellationToken = default) =>
        await DocumentationBlockAsync(client, cancellationToken) is null;

    public async Task<bool> IsOnboarderAsync(Client client, CancellationToken cancellationToken = default)
    {
        var userType = await _scope.GetUserTypeAsync(cancellationToken);

        // Typeless (legacy) users keep their old access; see IOfficeScope.
        if (userType is null)
        {
            return true;
        }

        // Clients from before onboarding tracked who did it can be documented by any manager or marketer in their office.
        // A client a super admin onboarded is documented by the marketer they were assigned to.
        return UserTypeSlugs.CanCreateClients(userType)
            && (client.CreatedById is null || client.CreatedById == _currentUser.UserId || client.StaffId == _currentUser.UserId);
    }

    public async Task<string?> DocumentationBlockAsync(Client client, CancellationToken cancellationToken = default)
    {
        // A super admin can do whatever the onboarder can, on any client — each change is audited
        // (see RecordSuperAdminDocumentationAsync) and doesn't send an approved client back to pending.
        if (await _scope.GetUserTypeAsync(cancellationToken) == UserTypeSlugs.SuperAdmin)
        {
            return null;
        }

        if (!await IsOnboarderAsync(client, cancellationToken))
        {
            return "Only the staff member who onboarded this client can change their documentation.";
        }

        if (await _scope.GetUserTypeAsync(cancellationToken) is null)
        {
            return null;
        }

        if (!ClientDeletionRules.WasApproved(client) || client.EditPrivilegeRequestId is not null)
        {
            return null;
        }

        return await IsAwaitingBackfillAsync(client, cancellationToken)
            ? null
            : "This client is approved, so their profile is locked. Request edit privilege from a controller to change it.";
    }

    /// <summary>
    /// A client from before the onboarding flow (no "Client onboarding" audit entry) was approved
    /// without the documentation the flow now collects. Until that's caught up — at least one
    /// document plus the minimum guarantors and references — the staff they're assigned to can fill
    /// it in without an edit privilege, and doing so doesn't send the client back to pending. After
    /// that the usual approved-client lock applies.
    /// </summary>
    private async Task<bool> IsAwaitingBackfillAsync(Client client, CancellationToken cancellationToken)
    {
        var onboardedThroughTheFlow = await _db.AuditTrail.AnyAsync(
            a => a.Module == nameof(Client) && a.Name == "Client onboarding" && a.EntityId == client.Id, cancellationToken);
        if (onboardedThroughTheFlow)
        {
            return false;
        }

        // Only documents whose file is actually in storage — many legacy rows point at files that
        // were never carried over, and those don't count as documentation.
        var locations = await _db.Documents
            .Where(d => d.Type == ReferenceEntityType.Client && d.RecordId == client.Id && d.Location != null)
            .Select(d => d.Location)
            .ToListAsync(cancellationToken);
        var hasDocument = false;
        foreach (var location in locations)
        {
            if (await _files.ExistsAsync(location, cancellationToken))
            {
                hasDocument = true;
                break;
            }
        }

        if (!hasDocument)
        {
            return true;
        }

        var counts = await _db.ClientContacts
            .Where(c => c.ClientId == client.Id)
            .GroupBy(c => c.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Kind, x => x.Count, cancellationToken);

        return counts.GetValueOrDefault(ClientContact.GuarantorKind) < ClientContact.MinimumGuarantors
            || counts.GetValueOrDefault(ClientContact.ReferenceKind) < ClientContact.MinimumReferences;
    }

    public async Task RecordSuperAdminDocumentationAsync(Client client, string section, string action, string? details = null, CancellationToken cancellationToken = default)
    {
        if (await _scope.GetUserTypeAsync(cancellationToken) != UserTypeSlugs.SuperAdmin)
        {
            return;
        }

        _db.AuditTrail.Add(new AuditTrailEntry
        {
            UserId = _currentUser.UserId,
            OfficeId = client.OfficeId,
            Module = nameof(Client),
            Action = $"{section}: {action} (super admin)",
            Name = "Client documentation",
            EntityId = client.Id,
            Notes = details,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NoteEditedAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client?.EditPrivilegeRequestId is null || client.Status != ClientStatus.Active)
        {
            return;
        }

        // A super admin's edit is an oversight correction, not the requester using their privilege.
        if (await _scope.GetUserTypeAsync(cancellationToken) == UserTypeSlugs.SuperAdmin)
        {
            return;
        }

        client.Status = ClientStatus.Pending;
        var request = await _db.ClientEditRequests.FirstOrDefaultAsync(r => r.Id == client.EditPrivilegeRequestId, cancellationToken);
        if (request is not null)
        {
            request.FirstEditedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
        }

        _db.AuditTrail.Add(new AuditTrailEntry
        {
            UserId = _currentUser.UserId,
            OfficeId = client.OfficeId,
            Module = nameof(Client),
            Action = "Edited — back to pending approval",
            Name = "Client edit privilege",
            EntityId = client.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientActions> ActionsForAsync(Client client, CancellationToken cancellationToken = default)
    {
        var userType = await _scope.GetUserTypeAsync(cancellationToken);
        var canDocument = await CanDocumentAsync(client, cancellationToken);
        var canApprove = UserTypeSlugs.CanApproveClients(userType);
        var pendingRequest = await _db.DeletionRequests.AnyAsync(
            r => r.EntityType == DeletionRequest.ClientEntity && r.EntityId == client.Id && r.Status == DeletionRequestStatus.Pending,
            cancellationToken);

        var blockers = await ApprovalBlockersAsync(client, cancellationToken);
        var pendingEditRequest = await _db.ClientEditRequests.AnyAsync(
            r => r.ClientId == client.Id && r.Status == ClientEditRequest.PendingStatus, cancellationToken);
        var isOnboarder = userType is not null && await IsOnboarderAsync(client, cancellationToken);

        return new ClientActions(
            CanEditDetails: canDocument,
            CanApprove: canApprove && client.Status == ClientStatus.Pending && !client.IsHighRisk && blockers.Count == 0,
            CanDecline: canApprove && client.Status == ClientStatus.Pending,
            CanDelete: (canDocument || canApprove) && ClientDeletionRules.CanDeleteDirectly(client),
            CanRequestDeletion: userType != UserTypeSlugs.SuperAdmin && ClientDeletionRules.WasApproved(client) && !pendingRequest,
            CanMarkSafe: userType == UserTypeSlugs.SuperAdmin && client.IsHighRisk,
            CanRequestEdit: isOnboarder && !canDocument && ClientDeletionRules.WasApproved(client) && client.EditPrivilegeRequestId is null && !pendingEditRequest
                && client.Status is ClientStatus.Active or ClientStatus.Inactive,
            EditPrivilegeOpen: client.EditPrivilegeRequestId is not null,
            CanReviewEditRequests: UserTypeSlugs.CanReviewEditRequests(userType) && pendingEditRequest,
            ApprovalBlockers: blockers,
            PendingDeletionRequest: pendingRequest);
    }

    // A List so EF can translate Contains (array.Contains binds to the span overload).
    private static readonly List<LoanStatus> OpenLoanStatuses =
    [
        LoanStatus.New, LoanStatus.Pending, LoanStatus.Approved, LoanStatus.NeedChanges,
        LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled,
    ];

    public async Task<string?> ActiveLoanAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var allocatedLoanIds = _db.GroupLoanAllocations.Where(a => a.ClientId == clientId && a.LoanId != null).Select(a => a.LoanId!.Value);
        var loan = await _db.Loans
            .Where(l => (l.ClientId == clientId || allocatedLoanIds.Contains(l.Id)) && OpenLoanStatuses.Contains(l.Status))
            .OrderByDescending(l => l.Id)
            .Select(l => new { l.Id, l.AccountNumber, l.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (loan is not null)
        {
            var what = loan.Status is LoanStatus.Disbursed or LoanStatus.PendingReschedule or LoanStatus.Rescheduled ? "is still running" : "is awaiting disbursement";
            return $"Loan {loan.AccountNumber ?? $"#{loan.Id}"} {what}.";
        }

        var application = await _db.LoanApplications
            .Where(a => a.ClientId == clientId && a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return application is null ? null : $"Loan application #{application} is awaiting a decision.";
    }

    public Task<bool> BvnTakenAsync(string bvn, int? exceptClientId, CancellationToken cancellationToken = default)
    {
        var value = bvn.Trim();
        return _db.Clients.AnyAsync(c => c.Bvn == value && c.DeletedAt == null && c.Id != exceptClientId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ApprovalBlockersAsync(Client client, CancellationToken cancellationToken = default)
    {
        var counts = await _db.ClientContacts
            .Where(c => c.ClientId == client.Id)
            .GroupBy(c => c.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Kind, x => x.Count, cancellationToken);

        var blockers = new List<string>();
        var guarantorsShort = ClientContact.MinimumGuarantors - counts.GetValueOrDefault(ClientContact.GuarantorKind);
        if (guarantorsShort > 0)
        {
            blockers.Add(guarantorsShort == 1 ? "1 more guarantor" : $"{guarantorsShort} more guarantors");
        }

        var referencesShort = ClientContact.MinimumReferences - counts.GetValueOrDefault(ClientContact.ReferenceKind);
        if (referencesShort > 0)
        {
            blockers.Add(referencesShort == 1 ? "1 reference" : $"{referencesShort} references");
        }

        if (client.BiometricEnrolledAt is null && await FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken))
        {
            blockers.Add("a face capture");
        }

        return blockers;
    }
}
