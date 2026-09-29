using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Groups;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/groups")]
[Authorize]
public class GroupsController : ControllerBase
{
    private const string ManagePolicy = "Permission:groups.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IGroupService _groupService;

    private readonly IOfficeScope _scope;

    public GroupsController(BCKashDbContext db, IGroupService groupService, IOfficeScope scope)
    {
        _scope = scope;
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
        query = await ScopedAsync(query, cancellationToken);

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
            .OrderByDescending(g => g.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();
        var memberCounts = await _db.GroupClients
            .Where(gc => gc.GroupId != null && groupIds.Contains(gc.GroupId.Value) && gc.RemovedAt == null)
            .GroupBy(gc => gc.GroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);
        var staffIds = groups.Where(g => g.StaffId.HasValue).Select(g => g.StaffId!.Value).Distinct().ToList();
        var staffNames = await _db.Users
            .Where(u => staffIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } name ? name : u.Email, cancellationToken);

        var items = groups
            .Select(g => ToListItemResponse(g) with
            {
                StaffName = g.StaffId.HasValue ? staffNames.GetValueOrDefault(g.StaffId.Value) : null,
                MemberCount = memberCounts.GetValueOrDefault(g.Id),
            })
            .ToList();
        return Ok(new PagedResult<GroupListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GroupResponse>> Get(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var group = await _db.Groups.FindAsync([id], cancellationToken);
        return group is null ? NotFound() : Ok(ToResponse(group));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

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
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

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
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!UserTypeSlugs.CanApproveClients(await _scope.GetUserTypeAsync(cancellationToken)))
        {
            return Problem(title: "Only controllers can approve groups.", statusCode: StatusCodes.Status403Forbidden);
        }

        // A group is approved only once all its members are — which happens automatically as the last one is.
        var unapproved = await _db.GroupClients
            .Where(gc => gc.GroupId == id && gc.RemovedAt == null)
            .CountAsync(gc => gc.Client!.Status != ClientStatus.Active, cancellationToken);
        if (unapproved > 0)
        {
            return Problem(title: $"{unapproved} member(s) still need approving. The group is approved once all its members are.", statusCode: StatusCodes.Status409Conflict);
        }

        var result = await _groupService.ActivateAsync(id, request?.ActivatedDate, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _groupService.DeactivateAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _groupService.ReactivateAsync(id, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>Bulk action: hands the ticked groups, and every client currently in them, to a marketer in each group's office.</summary>
    [HttpPost("bulk/reassign-marketer")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkReassignMarketer(BulkReassignMarketerRequest request, CancellationToken cancellationToken)
    {
        if (request.GroupIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one group.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await RunBulkAsync(request.GroupIds, id => _groupService.ReassignMarketerAsync(id, request.MarketerId, cancellationToken), cancellationToken));
    }

    /// <summary>Bulk action: disables the ticked active groups.</summary>
    [HttpPost("bulk/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkDeactivate(BulkDeactivateGroupsRequest request, CancellationToken cancellationToken)
    {
        if (request.GroupIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one group.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Problem(title: "A reason is required to disable groups.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await RunBulkAsync(request.GroupIds, id => _groupService.DeactivateAsync(id, request.Reason.Trim(), cancellationToken), cancellationToken));
    }

    /// <summary>Bulk action: re-enables the ticked disabled groups.</summary>
    [HttpPost("bulk/reactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkReactivate(BulkGroupsRequest request, CancellationToken cancellationToken)
    {
        if (request.GroupIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one group.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await RunBulkAsync(request.GroupIds, id => _groupService.ReactivateAsync(id, cancellationToken), cancellationToken));
    }

    /// <summary>The ticked groups the caller can see, newest first, each put through <paramref name="action"/>.</summary>
    private async Task<BulkActionResponse> RunBulkAsync(IReadOnlyList<int> groupIds, Func<int, Task<GroupWriteResult>> action, CancellationToken cancellationToken)
    {
        var ids = groupIds.Distinct().ToList();
        var groups = await (await ScopedAsync(_db.Groups.Where(g => ids.Contains(g.Id)), cancellationToken))
            .OrderByDescending(g => g.Id)
            .ToListAsync(cancellationToken);

        return await BulkActionRunner.RunAsync(
            groups,
            ids,
            g => g.Id,
            g => g.Name ?? g.AccountNo ?? $"Group #{g.Id}",
            async g => (await action(g.Id)).Outcome switch
            {
                GroupWriteOutcome.Success => null,
                GroupWriteOutcome.InvalidTransition => g.Status switch
                {
                    GroupStatus.Active => "Already enabled.",
                    GroupStatus.Inactive => "Already disabled.",
                    _ => $"Can't be changed while {g.Status.ToString().ToLowerInvariant()}.",
                },
                GroupWriteOutcome.MarketerNotFound => "The marketer isn't an active marketer.",
                GroupWriteOutcome.MarketerInDifferentOffice => "The marketer works in a different office.",
                GroupWriteOutcome.AlreadyAssigned => "Already assigned to this marketer.",
                GroupWriteOutcome.NotFound => "Not found.",
                _ => "Could not be updated.",
            },
            "Group");
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _groupService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _groupService.CloseAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<GroupSummaryResponse>> Summary(int id, [FromServices] IDeletionService deletions, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var group = await _db.Groups.FirstAsync(g => g.Id == id, cancellationToken);
        var memberships = await _db.GroupClients
            .Where(gc => gc.GroupId == id && gc.RemovedAt == null && gc.Client!.DeletedAt == null)
            .Select(gc => new { gc.Id, gc.Role, gc.Client })
            .ToListAsync(cancellationToken);
        var clientIds = memberships.Select(m => m.Client!.Id).ToList();

        // Every loan touching the group: members' own loans, the group's loans, and members' shares of them.
        var allocations = await _db.GroupLoanAllocations
            .Where(a => a.ClientId.HasValue && a.LoanId.HasValue && (clientIds.Contains(a.ClientId.Value) || a.GroupId == id))
            .Select(a => new { LoanId = a.LoanId!.Value, ClientId = a.ClientId!.Value, Amount = a.Amount ?? 0 })
            .ToListAsync(cancellationToken);
        var allocatedLoanIds = allocations.Select(a => a.LoanId).Distinct().ToList();
        var allLoans = await _db.Loans
            .Where(l => (l.ClientId.HasValue && clientIds.Contains(l.ClientId.Value)) || l.GroupId == id || allocatedLoanIds.Contains(l.Id))
            .OrderByDescending(l => l.Id)
            .ToListAsync(cancellationToken);
        bool PastApproval(Domain.Loans.Loan l) => l.ApprovedDate != null || ApprovedLoanStatuses.Contains(l.Status);

        // Loans that got past approval, whatever happened after — requested-only loans aren't counted.
        // A group loan counts towards each member by their allocated share.
        var loans = allLoans.Where(l => PastApproval(l) && l.ClientId.HasValue && clientIds.Contains(l.ClientId.Value))
            .Select(l => new { ClientId = l.ClientId!.Value, Amount = l.ApprovedAmount ?? l.Principal ?? 0 })
            .Concat(allocations
                .Where(a => clientIds.Contains(a.ClientId) && allLoans.Any(l => l.Id == a.LoanId && PastApproval(l) && !l.ClientId.HasValue))
                .Select(a => new { a.ClientId, a.Amount }))
            .ToList();

        var applications = await _db.LoanApplications
            .Where(a => ((a.ClientId.HasValue && clientIds.Contains(a.ClientId.Value)) || a.GroupId == id) && a.LoanId == null)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
        var names = await LoanDisplayNames.LoadAsync(
            _db,
            allLoans.Select(l => l.ClientId).Concat(applications.Select(a => a.ClientId)),
            allLoans.Select(l => l.GroupId).Concat(applications.Select(a => a.GroupId)),
            allLoans.Select(l => l.LoanProductId).Concat(applications.Select(a => (int?)a.LoanProductId)),
            [],
            cancellationToken);
        var loanRecords = allLoans
            .Select(l => new GroupLoanRecordResponse(
                "loan", l.Id, l.ClientId, names.Applicant(l.ClientId, l.GroupId), l.AccountNumber ?? $"#{l.Id}", names.Product(l.LoanProductId),
                l.ApprovedAmount ?? l.Principal ?? l.AppliedAmount ?? 0, l.Status.ToString(), l.DisbursementDate ?? l.ApprovedDate))
            .Concat(applications.Select(a => new GroupLoanRecordResponse(
                "application", a.Id, a.ClientId, names.Applicant(a.ClientId, a.GroupId), $"Application #{a.Id}", names.Product(a.LoanProductId),
                a.Amount, a.Status.ToString(), a.CreatedAt.HasValue ? DateOnly.FromDateTime(a.CreatedAt.Value) : null)))
            .OrderByDescending(r => r.Date)
            .ToList();
        var outstanding = await _db.LoanRepaymentSchedules
            .Where(s => s.Loan!.ClientId.HasValue && clientIds.Contains(s.Loan.ClientId.Value) && s.Loan.Status == LoanStatus.Disbursed)
            .GroupBy(s => s.Loan!.ClientId!.Value)
            .Select(g => new
            {
                ClientId = g.Key,
                Amount = g.Sum(s => (s.Principal ?? 0) - (s.PrincipalPaid ?? 0) + (s.Interest ?? 0) - (s.InterestPaid ?? 0)
                                  + (s.Fees ?? 0) - (s.FeesPaid ?? 0) - (s.FeesWaived ?? 0)
                                  + (s.Penalty ?? 0) - (s.PenaltyPaid ?? 0) - (s.PenaltyWaived ?? 0)),
            })
            .ToDictionaryAsync(x => x.ClientId, x => x.Amount, cancellationToken);

        var members = memberships
            .OrderBy(m => RoleOrder(m.Role))
            .ThenBy(m => m.Id)
            .Select(m => new GroupSummaryMemberResponse(
                m.Client!.Id,
                m.Client.DisplayName ?? $"{m.Client.FirstName} {m.Client.LastName}".Trim(),
                m.Client.AccountNo,
                m.Client.Status,
                m.Client.IsHighRisk,
                m.Role,
                loans.Where(l => l.ClientId == m.Client.Id).Sum(l => l.Amount),
                Math.Max(0, outstanding.GetValueOrDefault(m.Client.Id))))
            .ToList();

        var hasApprovedMember = group.ActivatedDate.HasValue || await deletions.GroupHasApprovedMemberAsync(id, cancellationToken);
        var pendingDeletion = await _db.DeletionRequests.AnyAsync(
            r => r.EntityType == DeletionRequest.GroupEntity && r.EntityId == id && r.Status == DeletionRequestStatus.Pending, cancellationToken);
        var userType = await _scope.GetUserTypeAsync(cancellationToken);
        var createdByName = group.CreatedById.HasValue
            ? await _db.Users.Where(u => u.Id == group.CreatedById).Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(cancellationToken)
            : null;
        var officeName = group.OfficeId.HasValue ? await _db.Offices.Where(o => o.Id == group.OfficeId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken) : null;

        return Ok(new GroupSummaryResponse(
            members.Sum(m => m.LoanAmount),
            loans.Count,
            members.Sum(m => m.PendingRepayment),
            members.Count,
            members.Count(m => m.Status == ClientStatus.Active),
            members.Count(m => m.Status == ClientStatus.Pending),
            members,
            CanDelete: !hasApprovedMember,
            CanRequestDeletion: hasApprovedMember && !pendingDeletion && userType != UserTypeSlugs.SuperAdmin,
            PendingDeletionRequest: pendingDeletion,
            CreatedByName: string.IsNullOrWhiteSpace(createdByName) ? null : createdByName,
            OfficeName: officeName,
            LoanRecords: loanRecords));
    }

    /// <summary>Deletes a group none of whose members has been approved. Its clients stay on the platform.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, [FromServices] IDeletionService deletions, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return ClientsController.DeletionResultFor(this, await deletions.DeleteGroupAsync(id, cancellationToken), "group");
    }

    /// <summary>Asks a super admin to delete a group with an approved member, giving the reason.</summary>
    [HttpPost("{id:int}/deletion-requests")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RequestDeletion(int id, ReasonRequest request, [FromServices] IDeletionService deletions, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return ClientsController.DeletionResultFor(this, await deletions.RequestAsync(DeletionRequest.GroupEntity, id, request.Reason, cancellationToken), "group");
    }

    // A List, not an array: array.Contains in a query binds to the span overload, which EF can't translate.
    private static readonly List<LoanStatus> ApprovedLoanStatuses =
    [
        LoanStatus.Approved, LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled,
        LoanStatus.Closed, LoanStatus.Paid, LoanStatus.WrittenOff,
    ];

    /// <summary>Leader, assistant, organizer, then everyone else in the order they joined — the roster's natural order.</summary>
    private static int RoleOrder(string? role) => role switch
    {
        GroupMemberRoles.Leader => 0,
        GroupMemberRoles.Assistant => 1,
        GroupMemberRoles.Organizer => 2,
        _ => 3,
    };

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

    /// <summary>Only records in the caller's offices (see <see cref="IOfficeScope"/>); everything for a super admin.</summary>
    private async Task<IQueryable<Group>> ScopedAsync(IQueryable<Group> query, CancellationToken cancellationToken)
    {
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        return officeIds is null ? query : query.Where(g => g.OfficeId.HasValue && officeIds.Contains(g.OfficeId.Value));
    }

    /// <summary>False for a group outside the caller's offices — callers answer 404, so it stays invisible.</summary>
    private async Task<bool> InScopeAsync(int id, CancellationToken cancellationToken) =>
        await (await ScopedAsync(_db.Groups.Where(g => g.Id == id), cancellationToken)).AnyAsync(cancellationToken);

    private ObjectResult OfficeOutOfScope() =>
        Problem(title: "You can only work with records in your own office(s).", statusCode: StatusCodes.Status403Forbidden);
}
