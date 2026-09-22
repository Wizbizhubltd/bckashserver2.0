using BCKash.Api.Contracts;
using BCKash.Application.Groups;
using BCKash.Domain.Groups;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController : ControllerBase
{
    private const string ManagePolicy = "Permission:groups.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IGroupService _groupService;

    public GroupsController(BCKashDbContext db, IGroupService groupService)
    {
        _db = db;
        _groupService = groupService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GroupListItemResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] int? officeId,
        [FromQuery] GroupStatus? status,
        [FromQuery] int? staffId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Groups.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(g => EF.Functions.Like(g.Name, pattern));
        }

        if (officeId.HasValue)
        {
            query = query.Where(g => g.OfficeId == officeId);
        }

        if (status.HasValue)
        {
            query = query.Where(g => g.Status == status);
        }

        if (staffId.HasValue)
        {
            query = query.Where(g => g.StaffId == staffId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var groups = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = groups.Select(ToListItemResponse).ToList();
        return Ok(new PagedResult<GroupListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GroupResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var group = await _db.Groups.FindAsync([id], cancellationToken);
        return group is null ? NotFound() : Ok(ToResponse(group));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var group = new Group
        {
            OfficeId = request.OfficeId,
            Name = request.Name,
            ExternalId = request.ExternalId,
            StaffId = request.StaffId,
            JoinedDate = request.JoinedDate,
            Mobile = request.Mobile,
            Phone = request.Phone,
            Email = request.Email,
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            Region = request.Region,
            Address = request.Address,
            Notes = request.Notes,
        };

        var result = await _groupService.CreateAsync(group, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Group!.Id }, ToResponse(result.Group));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        var updated = new Group
        {
            OfficeId = request.OfficeId,
            Name = request.Name,
            ExternalId = request.ExternalId,
            StaffId = request.StaffId,
            JoinedDate = request.JoinedDate,
            Mobile = request.Mobile,
            Phone = request.Phone,
            Email = request.Email,
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            Region = request.Region,
            Address = request.Address,
            Notes = request.Notes,
        };

        var result = await _groupService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome == GroupWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Group!));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, ActivateGroupRequest? request, CancellationToken cancellationToken)
    {
        var result = await _groupService.ActivateAsync(id, request?.ActivatedDate, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _groupService.DeactivateAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _groupService.ReactivateAsync(id, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _groupService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _groupService.CloseAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    private IActionResult ToTransitionResult(GroupWriteResult result) => result.Outcome switch
    {
        GroupWriteOutcome.Success => Ok(ToResponse(result.Group!)),
        GroupWriteOutcome.NotFound => NotFound(),
        GroupWriteOutcome.InvalidTransition => Problem(
            title: "This transition isn't valid from the group's current status.",
            statusCode: StatusCodes.Status400BadRequest),
        GroupWriteOutcome.ReasonRequired => Problem(
            title: "A reason is required for this transition.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static GroupListItemResponse ToListItemResponse(Group g) =>
        new(g.Id, g.AccountNo, g.Name, g.OfficeId, g.StaffId, g.Status, g.JoinedDate);

    private static GroupResponse ToResponse(Group g) => new(
        g.Id, g.OldGroupId, g.OfficeId, g.Name, g.AccountNo, g.ExternalId, g.StaffId,
        g.JoinedDate, g.Status,
        g.ActivatedDate, g.ActivatedById, g.ReactivatedDate, g.ReactivatedById,
        g.DeclinedDate, g.DeclinedById, g.DeclinedReason,
        g.ClosedDate, g.ClosedById, g.ClosedReason,
        g.InactiveDate, g.InactiveById, g.InactiveReason,
        g.Mobile, g.Phone, g.Email,
        g.Street, g.Ward, g.District, g.Region, g.Address, g.Notes);
}
