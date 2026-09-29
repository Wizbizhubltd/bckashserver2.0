using BCKash.Application.Clients;
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

    public ClientAccess(BCKashDbContext db, IOfficeScope scope, ICurrentUserContext currentUser)
    {
        _db = db;
        _scope = scope;
        _currentUser = currentUser;
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
        return UserTypeSlugs.CanCreateClients(userType) && (client.CreatedById is null || client.CreatedById == _currentUser.UserId);
    }

    public async Task<string?> DocumentationBlockAsync(Client client, CancellationToken cancellationToken = default)
    {
        if (!await IsOnboarderAsync(client, cancellationToken))
        {
            return "Only the staff member who onboarded this client can change their documentation.";
        }

        if (await _scope.GetUserTypeAsync(cancellationToken) is null)
        {
            return null;
        }

        return ClientDeletionRules.WasApproved(client) && client.EditPrivilegeRequestId is null
            ? "This client is approved, so their profile is locked. Request edit privilege from a controller to change it."
            : null;
    }

    public async Task NoteEditedAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client?.EditPrivilegeRequestId is null || client.Status != ClientStatus.Active)
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
            CanRequestEdit: isOnboarder && ClientDeletionRules.WasApproved(client) && client.EditPrivilegeRequestId is null && !pendingEditRequest
                && client.Status is ClientStatus.Active or ClientStatus.Inactive,
            EditPrivilegeOpen: client.EditPrivilegeRequestId is not null,
            CanReviewEditRequests: UserTypeSlugs.CanReviewEditRequests(userType) && pendingEditRequest);
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
