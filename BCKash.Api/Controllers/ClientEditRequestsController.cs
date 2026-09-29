using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Edit privilege for approved clients (see ClientEditRequest). The staff member who onboarded the
/// client asks with a reason; a controller in the client's office grants or refuses it. Granting
/// unlocks the client's documentation; the first saved edit sends them back to Pending, and approving
/// them again closes the privilege.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ClientEditRequestsController : ControllerBase
{
    private readonly BCKashDbContext _db;
    private readonly IClientAccess _access;
    private readonly IOfficeScope _scope;
    private readonly ICurrentUserContext _currentUser;

    public ClientEditRequestsController(BCKashDbContext db, IClientAccess access, IOfficeScope scope, ICurrentUserContext currentUser)
    {
        _db = db;
        _access = access;
        _scope = scope;
        _currentUser = currentUser;
    }

    /// <summary>The client's edit requests, newest first.</summary>
    [HttpGet("clients/{clientId:int}/edit-requests")]
    public async Task<ActionResult<IReadOnlyList<ClientEditRequestResponse>>> ForClient(int clientId, CancellationToken cancellationToken)
    {
        if (await _access.FindVisibleAsync(clientId, cancellationToken) is null)
        {
            return NotFound();
        }

        var requests = await _db.ClientEditRequests
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
        return Ok(await ToResponsesAsync(_db, requests, cancellationToken));
    }

    [HttpPost("clients/{clientId:int}/edit-requests")]
    public async Task<IActionResult> Request(int clientId, ReasonRequest request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(clientId, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        if (!(await _access.ActionsForAsync(client, cancellationToken)).CanRequestEdit)
        {
            var reason = !await _access.IsOnboarderAsync(client, cancellationToken)
                ? "Only the staff member who onboarded this client can request edit privilege."
                : !ClientDeletionRules.WasApproved(client)
                    ? "This client isn't approved yet, so their profile can still be edited."
                    : client.EditPrivilegeRequestId is not null
                        ? "Edit privilege is already open for this client."
                        : "An edit request for this client is already waiting for a controller.";
            return Problem(title: reason, statusCode: StatusCodes.Status409Conflict);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Problem(title: "Say why the client's profile needs editing.", statusCode: StatusCodes.Status400BadRequest);
        }

        var editRequest = new ClientEditRequest
        {
            ClientId = clientId,
            Reason = request.Reason.Trim(),
            Status = ClientEditRequest.PendingStatus,
            RequestedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ClientEditRequests.Add(editRequest);
        await _db.SaveChangesAsync(cancellationToken);
        await notifications.EditRequestRaisedAsync(editRequest, client, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, (await ToResponsesAsync(_db, [editRequest], cancellationToken)).Single());
    }

    /// <summary>Edit requests for clients in the caller's offices, newest first — by default those waiting for a controller.</summary>
    [HttpGet("client-edit-requests")]
    public async Task<ActionResult<PagedResult<ClientEditRequestResponse>>> List(
        [FromQuery] string? status = ClientEditRequest.PendingStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        var clients = _db.Clients.Where(c => c.DeletedAt == null);
        if (officeIds is not null)
        {
            clients = clients.Where(c => c.OfficeId.HasValue && officeIds.Contains(c.OfficeId.Value));
        }

        var query = _db.ClientEditRequests.Where(r => clients.Any(c => c.Id == r.ClientId));
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<ClientEditRequestResponse>(await ToResponsesAsync(_db, requests, cancellationToken), page, pageSize, total));
    }

    [HttpPost("client-edit-requests/{id:int}/approve")]
    public Task<IActionResult> Approve(int id, ReviewDeletionRequest? request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: true, request?.Note, notifications, cancellationToken);

    /// <summary>Refusing needs a note, so the requester knows why.</summary>
    [HttpPost("client-edit-requests/{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ReviewDeletionRequest? request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request?.Note, notifications, cancellationToken);

    private async Task<IActionResult> ReviewAsync(int id, bool approve, string? note, IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        if (!UserTypeSlugs.CanReviewEditRequests(await _scope.GetUserTypeAsync(cancellationToken)))
        {
            return Problem(title: "Only controllers and super admins review edit requests.", statusCode: StatusCodes.Status403Forbidden);
        }

        var editRequest = await _db.ClientEditRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        var client = editRequest is null ? null : await _access.FindVisibleAsync(editRequest.ClientId, cancellationToken);
        if (editRequest is null || client is null)
        {
            return NotFound();
        }

        if (editRequest.Status != ClientEditRequest.PendingStatus)
        {
            return Problem(title: "This request has already been reviewed.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            return Problem(title: "Give a note explaining why the request is refused.", statusCode: StatusCodes.Status400BadRequest);
        }

        editRequest.Status = approve ? ClientEditRequest.ApprovedStatus : ClientEditRequest.RejectedStatus;
        editRequest.ReviewedById = _currentUser.UserId;
        editRequest.ReviewedAt = DateTime.UtcNow;
        editRequest.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        editRequest.UpdatedAt = DateTime.UtcNow;
        if (approve)
        {
            client.EditPrivilegeRequestId = editRequest.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await notifications.EditRequestReviewedAsync(editRequest, client, granted: approve, cancellationToken);
        return Ok((await ToResponsesAsync(_db, [editRequest], cancellationToken)).Single());
    }

    /// <summary>The request waiting for a controller, or the granted one still open — for the client page.</summary>
    internal static async Task<ClientEditRequestResponse?> CurrentForAsync(BCKashDbContext db, Client client, CancellationToken cancellationToken)
    {
        var current = await db.ClientEditRequests
            .Where(r => r.ClientId == client.Id && (r.Status == ClientEditRequest.PendingStatus || r.Id == client.EditPrivilegeRequestId))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return current is null ? null : (await ToResponsesAsync(db, [current], cancellationToken)).Single();
    }

    private static async Task<List<ClientEditRequestResponse>> ToResponsesAsync(BCKashDbContext db, IReadOnlyList<ClientEditRequest> requests, CancellationToken cancellationToken)
    {
        var clientIds = requests.Select(r => r.ClientId).Distinct().ToList();
        var clients = await db.Clients
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.DisplayName ?? (c.FirstName + " " + c.LastName), c.AccountNo, c.OfficeId })
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var officeIds = clients.Values.Where(c => c.OfficeId.HasValue).Select(c => c.OfficeId!.Value).Distinct().ToList();
        var offices = await db.Offices.Where(o => officeIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);
        var userIds = requests.SelectMany(r => new[] { r.RequestedById, r.ReviewedById }).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var names = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);

        return requests.Select(r =>
        {
            var client = clients.GetValueOrDefault(r.ClientId);
            return new ClientEditRequestResponse(
                r.Id, r.ClientId, client?.Name, client?.AccountNo,
                client?.OfficeId is { } officeId ? offices.GetValueOrDefault(officeId) : null,
                r.Reason, r.Status,
                r.RequestedById, r.RequestedById.HasValue ? names.GetValueOrDefault(r.RequestedById.Value) : null, r.CreatedAt,
                r.ReviewedById.HasValue ? names.GetValueOrDefault(r.ReviewedById.Value) : null, r.ReviewedAt, r.ReviewNote,
                r.FirstEditedAt, r.CompletedAt);
        }).ToList();
    }
}
