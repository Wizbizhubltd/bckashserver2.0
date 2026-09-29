using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Groups;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>Group membership roster/history (BR-GRP-2). "Remove" soft-removes — see GroupClient's and IGroupMembershipService's doc comments.</summary>
[ApiController]
[Route("api/v1/groups/{groupId:int}/members")]
[Authorize]
public class GroupMembersController : ControllerBase
{
    private const string ManagePolicy = "Permission:groups.manage";

    private readonly BCKashDbContext _db;
    private readonly IGroupMembershipService _membershipService;
    private readonly IOfficeScope _scope;

    public GroupMembersController(BCKashDbContext db, IGroupMembershipService membershipService, IOfficeScope scope)
    {
        _db = db;
        _membershipService = membershipService;
        _scope = scope;
    }

    /// <summary>
    /// Bulk action: moves the ticked clients out of their current group into this one. Clients with an
    /// open loan or application, defaulters, and clients from another office stay put and are reported back.
    /// </summary>
    [HttpPost("bulk-move")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkMove(int groupId, BulkMoveClientsRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one client.", statusCode: StatusCodes.Status400BadRequest);
        }

        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null || (officeIds is not null && !(group.OfficeId.HasValue && officeIds.Contains(group.OfficeId.Value))))
        {
            return NotFound();
        }

        var ids = request.ClientIds.Distinct().ToList();
        var clients = await _db.Clients
            .Where(c => ids.Contains(c.Id) && c.DeletedAt == null)
            .Where(c => officeIds == null || (c.OfficeId.HasValue && officeIds.Contains(c.OfficeId.Value)))
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

        return Ok(await BulkActionRunner.RunAsync(
            clients,
            ids,
            c => c.Id,
            c => $"{c.FirstName} {c.LastName}".Trim() is { Length: > 0 } name ? name : c.AccountNo ?? $"Client #{c.Id}",
            async c => (await _membershipService.MoveAsync(c.Id, groupId, cancellationToken)).Outcome switch
            {
                MembershipWriteOutcome.Success => null,
                MembershipWriteOutcome.HasActiveLoan => "Has an active loan.",
                MembershipWriteOutcome.Defaulter => "Is a defaulter.",
                MembershipWriteOutcome.DifferentOffice => "Is in a different office from the group.",
                MembershipWriteOutcome.AlreadyAMember => "Already in this group.",
                MembershipWriteOutcome.GroupNotOpen => "The group isn't taking members.",
                _ => "Not found.",
            },
            "Client"));
    }

    /// <summary>Defaults to the current roster (RemovedAt IS NULL); <paramref name="includeRemoved"/>=true returns the full membership history.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GroupMemberResponse>>> List(int groupId, [FromQuery] bool includeRemoved, CancellationToken cancellationToken)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
        {
            return NotFound();
        }

        var query = _db.GroupClients.Where(gc => gc.GroupId == groupId);
        if (!includeRemoved)
        {
            query = query.Where(gc => gc.RemovedAt == null);
        }

        var memberships = await query.OrderByDescending(gc => gc.Id).ToListAsync(cancellationToken);
        var clientIds = memberships.Select(m => m.ClientId).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var clientsById = await _db.Clients
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var items = memberships.Select(m => ToResponse(m, clientsById)).ToList();
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<GroupMemberResponse>> Add(int groupId, AddGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _membershipService.AddAsync(groupId, request.ClientId, cancellationToken);

        if (result.Outcome != MembershipWriteOutcome.Success)
        {
            return result.Outcome switch
            {
                MembershipWriteOutcome.GroupNotFound => NotFound(),
                MembershipWriteOutcome.ClientNotFound => NotFound(),
                MembershipWriteOutcome.AlreadyAMember => Problem(
                    title: "This client is already an active member of this group.",
                    statusCode: StatusCodes.Status409Conflict),
                _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        var client = await _db.Clients.FindAsync([request.ClientId], cancellationToken);
        var clientsById = client is null ? [] : new Dictionary<int, Client> { [client.Id] = client };
        return CreatedAtAction(nameof(List), new { groupId }, ToResponse(result.Membership!, clientsById));
    }

    [HttpDelete("{groupClientId:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Remove(int groupId, int groupClientId, CancellationToken cancellationToken)
    {
        var result = await _membershipService.RemoveAsync(groupId, groupClientId, cancellationToken);
        return result.Outcome switch
        {
            MembershipWriteOutcome.Success => NoContent(),
            MembershipWriteOutcome.MembershipNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static GroupMemberResponse ToResponse(GroupClient membership, IReadOnlyDictionary<int, Client> clientsById)
    {
        var client = membership.ClientId.HasValue && clientsById.TryGetValue(membership.ClientId.Value, out var c) ? c : null;
        return new GroupMemberResponse(
            membership.Id, membership.ClientId, client?.DisplayName, client?.AccountNo,
            membership.CreatedAt, membership.CreatedById, membership.RemovedAt, membership.RemovedById,
            membership.Role, client?.Status, client?.IsHighRisk ?? false);
    }
}
