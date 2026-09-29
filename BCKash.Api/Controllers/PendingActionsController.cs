using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Organization;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// The super admin's to-do list in the control portal: requests and records waiting on an approval —
/// deletion and edit requests, high-risk clients to mark safe, staff awaiting onboarding approval, clients
/// pending approval and loan applications awaiting a decision. Read live from the records themselves, so
/// an item drops off the moment anyone deals with it; the bell shows the total.
/// </summary>
[ApiController]
[Route("api/v1/pending-actions")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public class PendingActionsController : ControllerBase
{
    /// <summary>How many of each kind are listed; the group's count and link cover the rest.</summary>
    private const int ItemsPerGroup = 10;

    private readonly BCKashDbContext _db;

    public PendingActionsController(BCKashDbContext db)
    {
        _db = db;
    }

    /// <summary>What the bell shows.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<PendingActionsSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var counts = await CountsAsync(cancellationToken);
        return Ok(new PendingActionsSummaryResponse(counts.Sum()));
    }

    [HttpGet]
    public async Task<ActionResult<PendingActionsResponse>> List([FromServices] ICurrencyDisplayProvider currencyDisplay, CancellationToken cancellationToken)
    {
        var currency = await currencyDisplay.GetAsync(cancellationToken);
        var groups = new List<PendingActionGroupResponse>();

        // Deletion requests (clients and groups) — only a super admin can decide them.
        var deletions = DeletionRequestsQuery();
        groups.Add(new PendingActionGroupResponse(
            "deletion_requests", "Deletion requests", "/deletion-requests", await deletions.CountAsync(cancellationToken),
            (await deletions.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(ItemsPerGroup).ToListAsync(cancellationToken))
                .Select(r => new PendingActionItemResponse(
                    r.Id,
                    $"Delete {r.EntityType} {r.EntityName ?? $"#{r.EntityId}"}",
                    r.Reason,
                    "/deletion-requests",
                    r.CreatedAt))
                .ToList()));

        // Client edit requests.
        var edits = EditRequestsQuery();
        var editItems = await edits
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(ItemsPerGroup)
            .Join(_db.Clients, r => r.ClientId, c => c.Id, (r, c) => new { r.Id, r.Reason, r.CreatedAt, c.DisplayName, c.FirstName, c.LastName })
            .ToListAsync(cancellationToken);
        groups.Add(new PendingActionGroupResponse(
            "edit_requests", "Client edit requests", "/edit-requests", await edits.CountAsync(cancellationToken),
            editItems.Select(r => new PendingActionItemResponse(r.Id, $"Edit {Name(r.DisplayName, r.FirstName, r.LastName)}", r.Reason, "/edit-requests", r.CreatedAt)).ToList()));

        // High-risk clients — kept on details that differ from their BVN until a super admin marks them safe.
        var highRisk = HighRiskQuery();
        groups.Add(new PendingActionGroupResponse(
            "high_risk_clients", "High-risk clients to review", "/clients?risk=high", await highRisk.CountAsync(cancellationToken),
            (await highRisk.OrderByDescending(c => c.HighRiskFlaggedAt ?? c.CreatedAt).ThenByDescending(c => c.Id).Take(ItemsPerGroup).ToListAsync(cancellationToken))
                .Select(c => new PendingActionItemResponse(c.Id, Name(c.DisplayName, c.FirstName, c.LastName), c.HighRiskReason, $"/clients/{c.Id}", c.HighRiskFlaggedAt ?? c.CreatedAt))
                .ToList()));

        // Staff awaiting onboarding approval.
        var staff = StaffOnboardingQuery();
        groups.Add(new PendingActionGroupResponse(
            "staff_onboarding", "Staff awaiting approval", "/staff?onboardingStatus=Pending", await staff.CountAsync(cancellationToken),
            (await staff.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).Take(ItemsPerGroup).Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.CreatedAt, Office = u.Office!.Name }).ToListAsync(cancellationToken))
                .Select(u => new PendingActionItemResponse(u.Id, Name(null, u.FirstName, u.LastName, u.Email), u.Office, $"/staff/{u.Id}", u.CreatedAt))
                .ToList()));

        // Clients pending approval (high-risk ones are listed above).
        var clients = PendingClientsQuery();
        groups.Add(new PendingActionGroupResponse(
            "clients_pending", "Clients pending approval", "/clients?status=Pending", await clients.CountAsync(cancellationToken),
            (await clients.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(ItemsPerGroup).Select(c => new { c.Id, c.DisplayName, c.FirstName, c.LastName, c.AccountNo, c.CreatedAt, Office = c.Office!.Name }).ToListAsync(cancellationToken))
                .Select(c => new PendingActionItemResponse(
                    c.Id, Name(c.DisplayName, c.FirstName, c.LastName),
                    string.Join(" · ", new[] { c.AccountNo, c.Office }.Where(p => !string.IsNullOrWhiteSpace(p))), $"/clients/{c.Id}", c.CreatedAt))
                .ToList()));

        // Loan applications awaiting a decision.
        var applications = PendingApplicationsQuery();
        var applicationItems = await applications.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(ItemsPerGroup).ToListAsync(cancellationToken);
        var names = await LoanDisplayNames.LoadAsync(
            _db, applicationItems.Select(a => a.ClientId), applicationItems.Select(a => a.GroupId), applicationItems.Select(a => (int?)a.LoanProductId), applicationItems.Select(a => a.OfficeId), cancellationToken);
        groups.Add(new PendingActionGroupResponse(
            "loan_applications", "Loan applications", "/loan-applications?status=Pending", await applications.CountAsync(cancellationToken),
            applicationItems
                .Select(a => new PendingActionItemResponse(
                    a.Id,
                    $"{names.Applicant(a.ClientId, a.GroupId) ?? $"Application #{a.Id}"} — {currency.Format(a.Amount)}",
                    string.Join(" · ", new[] { names.Product(a.LoanProductId), names.Office(a.OfficeId) }.Where(p => !string.IsNullOrWhiteSpace(p))),
                    $"/loan-applications/{a.Id}",
                    a.CreatedAt))
                .ToList()));

        var nonEmpty = groups.Where(g => g.Count > 0).ToList();
        return Ok(new PendingActionsResponse(nonEmpty.Sum(g => g.Count), nonEmpty));
    }

    private async Task<int[]> CountsAsync(CancellationToken cancellationToken) =>
    [
        await DeletionRequestsQuery().CountAsync(cancellationToken),
        await EditRequestsQuery().CountAsync(cancellationToken),
        await HighRiskQuery().CountAsync(cancellationToken),
        await StaffOnboardingQuery().CountAsync(cancellationToken),
        await PendingClientsQuery().CountAsync(cancellationToken),
        await PendingApplicationsQuery().CountAsync(cancellationToken),
    ];

    private IQueryable<DeletionRequest> DeletionRequestsQuery() => _db.DeletionRequests.Where(r => r.Status == DeletionRequestStatus.Pending);

    private IQueryable<ClientEditRequest> EditRequestsQuery() => _db.ClientEditRequests.Where(r => r.Status == ClientEditRequest.PendingStatus);

    private IQueryable<Client> HighRiskQuery() => _db.Clients.Where(c => c.DeletedAt == null && c.IsHighRisk);

    private IQueryable<User> StaffOnboardingQuery() => _db.Users.Where(u => u.OnboardingStatus == UserOnboardingStatus.Pending);

    private IQueryable<Client> PendingClientsQuery() => _db.Clients.Where(c => c.DeletedAt == null && c.Status == ClientStatus.Pending && !c.IsHighRisk);

    private IQueryable<LoanApplication> PendingApplicationsQuery() => _db.LoanApplications.Where(a => a.Status == ApprovalStatus.Pending);

    private static string Name(string? displayName, string? firstName, string? lastName, string? fallback = null) =>
        !string.IsNullOrWhiteSpace(displayName) ? displayName
        : $"{firstName} {lastName}".Trim() is { Length: > 0 } name ? name
        : fallback ?? "Unnamed";
}
