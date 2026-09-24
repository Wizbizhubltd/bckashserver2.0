using BCKash.Api.Contracts;
using BCKash.Application.Groups;
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

    public GroupMembersController(BCKashDbContext db, IGroupMembershipService membershipService)
    {
        _db = db;
        _membershipService = membershipService;
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

        var memberships = await query.ToListAsync(cancellationToken);
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
            membership.CreatedAt, membership.CreatedById, membership.RemovedAt, membership.RemovedById);
    }
}
