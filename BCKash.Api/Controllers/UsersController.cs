using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Identity;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Staff onboarding and management (see docs/staff-onboarding-rbac-spec.md). Every mutating
/// action requires the `users.manage` permission at the HTTP layer; the maker-checker rule
/// (UserClass Initiator creates, a same-user_type Authorizer approves/declines, a super admin
/// bypasses both) is enforced inside IUserService, since it depends on per-record context a
/// declarative policy attribute can't express.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private const string ManagePolicy = "Permission:users.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IUserService _userService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOfficeScope _scope;

    public UsersController(BCKashDbContext db, IUserService userService, ICurrentUserContext currentUser, IOfficeScope scope)
    {
        _db = db;
        _scope = scope;
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PagedResult<UserResponse>>> List(
        [FromQuery] int? officeId,
        [FromQuery] string? userType,
        [FromQuery] UserOnboardingStatus? onboardingStatus,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = await FilteredUsersAsync(officeId, userType, onboardingStatus, search, cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var responses = new List<UserResponse>();
        foreach (var user in users)
        {
            responses.Add(await ToResponseAsync(user, cancellationToken));
        }

        return Ok(new PagedResult<UserResponse>(responses, page, pageSize, totalCount));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = _currentUser.UserId.HasValue
            ? await _db.Users.Include(u => u.Office).FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
            : null;
        return user is null ? NotFound() : Ok(await ToResponseAsync(user, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<UserResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var user = await (await ScopedUsersAsync(cancellationToken)).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? NotFound() : Ok(await ToResponseAsync(user, cancellationToken));
    }

    /// <summary>The signed-in user updates their own profile and completes their onboarding details. No permission needed beyond being signed in.</summary>
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return Problem(title: "First and last name are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (OnboardingDetailsProblem(request.BankAccountNumber, request.DateOfBirth) is { } problem)
        {
            return problem;
        }

        var updated = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = Clean(request.Phone),
            Gender = request.Gender ?? Gender.Unspecified,
            Address = Clean(request.Address),
            DateOfBirth = request.DateOfBirth,
            NextOfKinName = Clean(request.NextOfKinName),
            NextOfKinPhone = Clean(request.NextOfKinPhone),
            NextOfKinRelationship = Clean(request.NextOfKinRelationship),
            BankName = Clean(request.BankName),
            BankAccountNumber = Clean(request.BankAccountNumber),
            BankAccountName = Clean(request.BankAccountName),
        };

        var result = await _userService.UpdateOwnProfileAsync(updated, cancellationToken);
        if (result.Outcome != UserWriteOutcome.Success)
        {
            return NotFound();
        }

        var user = await _db.Users.Include(u => u.Office).FirstAsync(u => u.Id == result.User!.Id, cancellationToken);
        return Ok(await ToResponseAsync(user, cancellationToken));

        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Super admins edit a staff member's whole record, including the onboarding details staff normally fill in themselves.</summary>
    [HttpPut("{id:int}/record")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> UpdateRecord(int id, UpdateStaffRecordRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            return Problem(title: "A valid email address is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return Problem(title: "First and last name are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (OnboardingDetailsProblem(request.BankAccountNumber, request.DateOfBirth) is { } problem)
        {
            return problem;
        }

        var updated = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = Clean(request.Phone),
            Gender = request.Gender ?? Gender.Unspecified,
            Address = Clean(request.Address),
            Notes = Clean(request.Notes),
            DateOfBirth = request.DateOfBirth,
            NextOfKinName = Clean(request.NextOfKinName),
            NextOfKinPhone = Clean(request.NextOfKinPhone),
            NextOfKinRelationship = Clean(request.NextOfKinRelationship),
            BankName = Clean(request.BankName),
            BankAccountNumber = Clean(request.BankAccountNumber),
            BankAccountName = Clean(request.BankAccountName),
        };

        var result = await _userService.UpdateRecordAsync(id, updated, cancellationToken);
        return result.Outcome switch
        {
            UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
            UserWriteOutcome.EmailInUse => Problem(title: "This email is already registered.", statusCode: StatusCodes.Status409Conflict),
            _ => ToScopeFailure(result.Outcome),
        };

        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The onboarding-detail rules shared by a staff member's own profile edit and a super admin's record edit.</summary>
    private ObjectResult? OnboardingDetailsProblem(string? bankAccountNumber, DateOnly? dateOfBirth)
    {
        if (bankAccountNumber?.Trim() is { Length: > 0 } accountNumber && (accountNumber.Length != 10 || !accountNumber.All(char.IsDigit)))
        {
            return Problem(title: "Bank account number must be 10 digits.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (dateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-16))
        {
            return Problem(title: "Date of birth must be at least 16 years ago.", statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    /// <summary>Super admins assign the zones a director oversees; the director then manages every office in them. Replaces the director's zones.</summary>
    [HttpPut("{id:int}/zones")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> AssignZones(int id, AssignZonesRequest request, CancellationToken cancellationToken) =>
        await ToZonesResultAsync(await _userService.AssignZonesAsync(id, request.ZoneIds ?? [], cancellationToken), cancellationToken);

    /// <summary>Bulk action: adds the listed zones to those a director already oversees, keeping the rest.</summary>
    [HttpPost("{id:int}/zones")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> AddZones(int id, AssignZonesRequest request, CancellationToken cancellationToken)
    {
        if (request.ZoneIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one zone.", statusCode: StatusCodes.Status400BadRequest);
        }

        var current = await _db.UserZones.Where(uz => uz.UserId == id).Select(uz => uz.ZoneId).ToListAsync(cancellationToken);
        return await ToZonesResultAsync(await _userService.AssignZonesAsync(id, current.Union(request.ZoneIds).ToList(), cancellationToken), cancellationToken);
    }

    private async Task<IActionResult> ToZonesResultAsync(UserWriteResult result, CancellationToken cancellationToken) =>
        result.Outcome switch
        {
            UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
            UserWriteOutcome.NotFound => NotFound(),
            UserWriteOutcome.NotADirector => Problem(title: "Zones can only be assigned to a director.", statusCode: StatusCodes.Status400BadRequest),
            UserWriteOutcome.ZoneNotFound => Problem(title: "One or more zones were not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };

    /// <summary>The staff member's own actions from the audit trail, newest first.</summary>
    [HttpGet("{id:int}/activity")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PagedResult<UserActivityResponse>>> Activity(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await (await ScopedUsersAsync(cancellationToken)).AnyAsync(u => u.Id == id, cancellationToken))
        {
            return NotFound();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.AuditTrail.Where(a => a.UserId == id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new UserActivityResponse(a.Id, a.Module, a.Action, a.Notes, a.OfficeId, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<UserActivityResponse>(items, page, pageSize, totalCount));
    }

    /// <summary>Initiates a new staff record. A super admin's creation is auto-approved; anyone else needs UserClass = Initiator and leaves it Pending for an Authorizer of the same user_type.</summary>
    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            OfficeId = request.OfficeId,
            UserClass = request.UserClass,
            Gender = request.Gender ?? Gender.Unspecified,
            Address = request.Address,
            Notes = request.Notes,
        };

        var result = await _userService.CreateAsync(user, request.UserTypeSlug, cancellationToken);
        return result.Outcome switch
        {
            UserWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.User!.Id }, await ToResponseAsync(result.User, cancellationToken)),
            UserWriteOutcome.EmailInUse => Problem(title: "This email is already registered.", statusCode: StatusCodes.Status409Conflict),
            UserWriteOutcome.OfficeNotFound => Problem(title: "Office not found.", statusCode: StatusCodes.Status400BadRequest),
            UserWriteOutcome.UserTypeNotFound => Problem(title: "Unknown user type.", statusCode: StatusCodes.Status400BadRequest),
            UserWriteOutcome.NotAuthorizedToInitiate => Problem(
                title: "Only a user_class Initiator (or a super admin) can create a new staff record.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => ToScopeFailure(result.Outcome),
        };
    }

    [HttpPost("{id:int}/approve-onboarding")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ApproveOnboarding(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.ApproveOnboardingAsync(id, cancellationToken);
        return await ToOnboardingResultAsync(result, cancellationToken);
    }

    [HttpPost("{id:int}/decline-onboarding")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DeclineOnboarding(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.DeclineOnboardingAsync(id, request.Reason, cancellationToken);
        return await ToOnboardingResultAsync(result, cancellationToken);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var updated = new User { FirstName = request.FirstName, LastName = request.LastName, Phone = request.Phone, Address = request.Address, Notes = request.Notes, Gender = request.Gender ?? Gender.Unspecified };
        var result = await _userService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : ToScopeFailure(result.Outcome);
    }

    [HttpPost("{id:int}/assign-office")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> AssignOffice(int id, AssignOfficeRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.AssignOfficeAsync(id, request.OfficeId, cancellationToken);
        return result.Outcome switch
        {
            UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
            UserWriteOutcome.NotFound => NotFound(),
            UserWriteOutcome.OfficeNotFound => Problem(title: "Office not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => ToScopeFailure(result.Outcome),
        };
    }

    [HttpPost("{id:int}/change-user-type")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ChangeUserType(int id, ChangeUserTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.ChangeUserTypeAsync(id, request.UserTypeSlug, cancellationToken);
        return result.Outcome switch
        {
            UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
            UserWriteOutcome.NotFound => NotFound(),
            UserWriteOutcome.UserTypeNotFound => Problem(title: "Unknown user type.", statusCode: StatusCodes.Status400BadRequest),
            _ => ToScopeFailure(result.Outcome),
        };
    }

    [HttpPost("{id:int}/change-user-class")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ChangeUserClass(int id, ChangeUserClassRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.ChangeUserClassAsync(id, request.UserClass, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : ToScopeFailure(result.Outcome);
    }

    [HttpPost("{id:int}/block")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Block(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.BlockAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : ToScopeFailure(result.Outcome);
    }

    [HttpPost("{id:int}/unblock")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Unblock(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.UnblockAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : ToScopeFailure(result.Outcome);
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.ResetPasswordAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : ToScopeFailure(result.Outcome);
    }

    /// <summary>
    /// Bulk action: forces the ticked staff (or, with All, every staff member matching the list
    /// filters) to reset their password — each is emailed an 8-character temporary password and the
    /// staff-portal address. Blocked staff and super admins are skipped and reported back.
    /// </summary>
    [HttpPost("bulk/reset-password")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkResetPassword(BulkResetPasswordRequest request, CancellationToken cancellationToken)
    {
        List<User> users;
        if (request.All)
        {
            var query = await FilteredUsersAsync(request.OfficeId, request.UserType, request.OnboardingStatus, request.Search, cancellationToken);
            users = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).ToListAsync(cancellationToken);
        }
        else if (request.UserIds is { Count: > 0 })
        {
            users = await SelectedUsersAsync(request.UserIds, cancellationToken);
        }
        else
        {
            return Problem(title: "Select at least one staff member.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await RunBulkAsync(users, request.All ? [] : request.UserIds!, id => _userService.ForcePasswordResetAsync(id, cancellationToken)));
    }

    /// <summary>
    /// Bulk action: transfers the ticked active staff to an office and emails each of them. Staff
    /// with a client or group on a running loan stay where they are and are reported back.
    /// </summary>
    [HttpPost("bulk/transfer")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkTransfer(BulkTransferRequest request, CancellationToken cancellationToken)
    {
        if (request.UserIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one staff member.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.Offices.AnyAsync(o => o.Id == request.OfficeId && o.Active && o.DeletedAt == null, cancellationToken))
        {
            return Problem(title: "Office not found or inactive.", statusCode: StatusCodes.Status400BadRequest);
        }

        var users = await SelectedUsersAsync(request.UserIds, cancellationToken);
        return Ok(await RunBulkAsync(users, request.UserIds, id => _userService.TransferOfficeAsync(id, request.OfficeId, cancellationToken)));
    }

    /// <summary>Bulk action: disables (blocks) the ticked active staff. Super admins are exempt and reported back.</summary>
    [HttpPost("bulk/disable")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<BulkActionResponse>> BulkDisable(BulkStaffIdsRequest request, CancellationToken cancellationToken)
    {
        if (request.UserIds is not { Count: > 0 })
        {
            return Problem(title: "Select at least one staff member.", statusCode: StatusCodes.Status400BadRequest);
        }

        var users = await SelectedUsersAsync(request.UserIds, cancellationToken);
        return Ok(await RunBulkAsync(users, request.UserIds, id => _userService.DisableAsync(id, cancellationToken)));
    }

    /// <summary>The ticked staff the caller can see, most recent first; ids they can't see are reported as not found by <see cref="RunBulkAsync"/>.</summary>
    private async Task<List<User>> SelectedUsersAsync(IReadOnlyList<int> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        return await (await ScopedUsersAsync(cancellationToken))
            .Where(u => ids.Contains(u.Id))
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    private static Task<BulkActionResponse> RunBulkAsync(IReadOnlyList<User> users, IReadOnlyList<int> requestedIds, Func<int, Task<UserWriteResult>> action) =>
        BulkActionRunner.RunAsync(
            users,
            requestedIds,
            u => u.Id,
            u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } fullName ? fullName : u.Email,
            async u => (await action(u.Id)).Outcome is var outcome && outcome == UserWriteOutcome.Success ? null : BulkSkipReason(outcome),
            "Staff");

    private static string BulkSkipReason(UserWriteOutcome outcome) => outcome switch
    {
        UserWriteOutcome.NotFound => "Not found.",
        UserWriteOutcome.NotActive => "Already blocked — only active staff are included.",
        UserWriteOutcome.SuperAdminExcluded => "Super admins are exempt from bulk actions.",
        UserWriteOutcome.HasActiveLoans => "Has clients with active loans.",
        UserWriteOutcome.AlreadyInOffice => "Already in this office.",
        UserWriteOutcome.OfficeNotFound => "Office not found or inactive.",
        UserWriteOutcome.OfficeOutOfScope => "You can only move staff into your own office(s).",
        UserWriteOutcome.NotAuthorizedToManage => "You can only manage staff ranked below you.",
        _ => "Could not be updated.",
    };

    private async Task<IActionResult> ToOnboardingResultAsync(UserWriteResult result, CancellationToken cancellationToken) => result.Outcome switch
    {
        UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
        UserWriteOutcome.NotFound => NotFound(),
        UserWriteOutcome.InvalidTransition => Problem(title: "This staff record is no longer pending.", statusCode: StatusCodes.Status400BadRequest),
        UserWriteOutcome.ReasonRequired => Problem(title: "A reason is required to decline.", statusCode: StatusCodes.Status400BadRequest),
        UserWriteOutcome.NotAuthorizedToAuthorize => Problem(
            title: "Only a user_class Authorizer of the same user_type as the initiator (or a super admin) can approve/decline this.",
            statusCode: StatusCodes.Status403Forbidden),
        _ => ToScopeFailure(result.Outcome),
    };

    /// <summary>The office-scope and seniority refusals every staff action can hit below a super admin.</summary>
    private IActionResult ToScopeFailure(UserWriteOutcome outcome) => outcome switch
    {
        UserWriteOutcome.NotFound => NotFound(),
        UserWriteOutcome.OfficeOutOfScope => Problem(title: "You can only manage staff in your own office(s).", statusCode: StatusCodes.Status403Forbidden),
        UserWriteOutcome.UserTypeNotPermitted => Problem(title: "You can only onboard or assign staff types ranked below your own.", statusCode: StatusCodes.Status403Forbidden),
        UserWriteOutcome.NotAuthorizedToManage => Problem(title: "You can only manage staff ranked below you.", statusCode: StatusCodes.Status403Forbidden),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    /// <summary>Staff the caller may see: everyone for a super admin, otherwise only staff in the caller's offices (see <see cref="IOfficeScope"/>).</summary>
    private async Task<IQueryable<User>> ScopedUsersAsync(CancellationToken cancellationToken)
    {
        var query = _db.Users.Include(u => u.Office).AsQueryable();
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        return officeIds is null ? query : query.Where(u => u.OfficeId.HasValue && officeIds.Contains(u.OfficeId.Value));
    }

    /// <summary>The staff list's filters, applied to the staff the caller may see.</summary>
    private async Task<IQueryable<User>> FilteredUsersAsync(int? officeId, string? userType, UserOnboardingStatus? onboardingStatus, string? search, CancellationToken cancellationToken)
    {
        var query = await ScopedUsersAsync(cancellationToken);
        if (officeId.HasValue)
        {
            query = query.Where(u => u.OfficeId == officeId);
        }

        if (onboardingStatus.HasValue)
        {
            query = query.Where(u => u.OnboardingStatus == onboardingStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.FirstName != null && u.FirstName.Contains(term))
                || (u.LastName != null && u.LastName.Contains(term))
                || (u.FirstName + " " + u.LastName).Contains(term)
                || u.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(userType))
        {
            var typeUserIds = await UserIdsForTypeAsync(userType, cancellationToken);
            query = query.Where(u => typeUserIds.Contains(u.Id));
        }

        return query;
    }

    private async Task<HashSet<int>> UserIdsForTypeAsync(string userTypeSlug, CancellationToken cancellationToken) =>
        (await _db.RoleUsers.Where(ru => ru.Role.Slug == userTypeSlug).Select(ru => ru.UserId).ToListAsync(cancellationToken)).ToHashSet();

    private async Task<UserResponse> ToResponseAsync(User u, CancellationToken cancellationToken)
    {
        var userType = await _db.RoleUsers
            .Where(ru => ru.UserId == u.Id)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

        var zones = await _db.UserZones
            .Where(uz => uz.UserId == u.Id)
            .OrderByDescending(uz => uz.CreatedAt)
            .Select(uz => new UserZoneResponse(uz.ZoneId, uz.Zone.Name))
            .ToListAsync(cancellationToken);
        var missingProfileFields = UserProfileRules.MissingFields(u);
        var tickedModules = userType is null
            ? []
            : await _db.RoleModules.Where(rm => rm.Role.Slug == userType).Select(rm => rm.Module).ToListAsync(cancellationToken);
        var modules = OfficePortalModules.All.Select(m => m.Slug).Where(tickedModules.Contains).ToList();

        var relatedIds = new[] { u.CreatedById, u.OnboardingApprovedById, u.UpdatedById }.Where(i => i.HasValue).Select(i => i!.Value).ToList();
        var relatedNames = await _db.Users
            .Where(r => relatedIds.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName, r.Email })
            .ToDictionaryAsync(
                r => r.Id,
                r => string.IsNullOrWhiteSpace($"{r.FirstName} {r.LastName}") ? r.Email : $"{r.FirstName} {r.LastName}".Trim(),
                cancellationToken);

        return new UserResponse(
            u.Id, u.Email, u.FirstName, u.LastName, u.Phone, u.OfficeId, u.Office?.Name, userType, u.UserClass, u.Blocked,
            u.OnboardingStatus, u.CreatedById, u.OnboardingApprovedById, u.OnboardingApprovedDate,
            u.OnboardingDeclinedById, u.OnboardingDeclinedDate, u.OnboardingDeclinedReason, u.LastLogin,
            u.Gender, u.Address, u.Notes, u.CreatedAt,
            u.CreatedById.HasValue ? relatedNames.GetValueOrDefault(u.CreatedById.Value) : null,
            u.OnboardingApprovedById.HasValue ? relatedNames.GetValueOrDefault(u.OnboardingApprovedById.Value) : null,
            u.UpdatedAt,
            u.UpdatedById.HasValue ? relatedNames.GetValueOrDefault(u.UpdatedById.Value) : null,
            u.DateOfBirth, u.NextOfKinName, u.NextOfKinPhone, u.NextOfKinRelationship,
            u.BankName, u.BankAccountNumber, u.BankAccountName,
            zones,
            modules,
            missingProfileFields,
            missingProfileFields.Count == 0,
            u.MustChangePassword,
            u.PasswordResetRequestedAt,
            u.PasswordChangedAt);
    }
}
