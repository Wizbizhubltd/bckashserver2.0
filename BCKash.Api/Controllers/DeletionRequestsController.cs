using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Requests to delete approved clients, or groups with an approved member (see IDeletionService).
/// Raised from the office portal; super admins review them here — approving carries the deletion out.
/// </summary>
[ApiController]
[Route("api/v1/deletion-requests")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public class DeletionRequestsController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IDeletionService _deletions;

    public DeletionRequestsController(BCKashDbContext db, IDeletionService deletions)
    {
        _db = db;
        _deletions = deletions;
    }

    /// <summary>Newest first; filter by status to see what's waiting, or by entity for one client's or group's requests.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DeletionRequestResponse>>> List(
        [FromQuery] DeletionRequestStatus? status,
        [FromQuery] string? entityType,
        [FromQuery] int? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.DeletionRequests.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(r => r.EntityType == entityType);
        }

        if (entityId.HasValue)
        {
            query = query.Where(r => r.EntityId == entityId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<DeletionRequestResponse>(await ToResponsesAsync(requests, cancellationToken), page, pageSize, totalCount));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ReviewDeletionRequest? request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        var response = await ReviewAsync(id, approve: true, request?.Note, cancellationToken);
        var deleted = await _db.DeletionRequests.Where(r => r.Id == id && r.Status == DeletionRequestStatus.Approved && r.EntityType == DeletionRequest.ClientEntity)
            .Select(r => (int?)r.EntityId)
            .FirstOrDefaultAsync(cancellationToken);
        if (deleted is { } clientId)
        {
            // The client is gone, so nothing about them waits on anyone's bell.
            await notifications.ClientRemovedAsync(clientId, cancellationToken);
        }

        return response;
    }

    /// <summary>Rejecting needs a note, so whoever raised the request knows why.</summary>
    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ReviewDeletionRequest? request, CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request?.Note, cancellationToken);

    private async Task<IActionResult> ReviewAsync(int id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var result = await _deletions.ReviewAsync(id, approve, note, cancellationToken);
        return result.Outcome switch
        {
            DeletionOutcome.Success => Ok((await ToResponsesAsync([result.Request!], cancellationToken)).Single()),
            DeletionOutcome.NotFound => NotFound(),
            DeletionOutcome.AlreadyReviewed => Problem(title: "This request has already been reviewed.", statusCode: StatusCodes.Status409Conflict),
            DeletionOutcome.ReasonRequired => Problem(title: "Give a note explaining why the request is rejected.", statusCode: StatusCodes.Status400BadRequest),
            DeletionOutcome.HasOpenLoans => Problem(
                title: $"The {result.Request?.EntityType ?? "record"} still has loans or loan applications open, so it can't be deleted yet.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private async Task<List<DeletionRequestResponse>> ToResponsesAsync(IReadOnlyList<DeletionRequest> requests, CancellationToken cancellationToken)
    {
        var userIds = requests.SelectMany(r => new[] { r.RequestedById, r.ReviewedById }).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);
        var officeIds = requests.Where(r => r.OfficeId.HasValue).Select(r => r.OfficeId!.Value).Distinct().ToList();
        var offices = await _db.Offices.Where(o => officeIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        return requests.Select(r => new DeletionRequestResponse(
            r.Id, r.EntityType, r.EntityId, r.EntityName, r.OfficeId, r.OfficeId.HasValue ? offices.GetValueOrDefault(r.OfficeId.Value) : null,
            r.Reason, r.Status,
            r.RequestedById, r.RequestedById.HasValue ? names.GetValueOrDefault(r.RequestedById.Value) : null, r.CreatedAt,
            r.ReviewedById, r.ReviewedById.HasValue ? names.GetValueOrDefault(r.ReviewedById.Value) : null, r.ReviewedAt, r.ReviewNote)).ToList();
    }
}
