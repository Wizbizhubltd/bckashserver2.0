using BCKash.Api.Contracts;
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

    public UsersController(BCKashDbContext db, IUserService userService, ICurrentUserContext currentUser)
    {
        _db = db;
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PagedResult<UserResponse>>> List(
        [FromQuery] int? officeId,
        [FromQuery] string? userType,
        [FromQuery] UserOnboardingStatus? onboardingStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Users.Include(u => u.Office).AsQueryable();
        if (officeId.HasValue)
        {
            query = query.Where(u => u.OfficeId == officeId);
        }

        if (onboardingStatus.HasValue)
        {
            query = query.Where(u => u.OnboardingStatus == onboardingStatus);
        }

        if (!string.IsNullOrWhiteSpace(userType))
        {
            var typeUserIds = await UserIdsForTypeAsync(userType, cancellationToken);
            query = query.Where(u => typeUserIds.Contains(u.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query.OrderByDescending(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

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
        var user = await _db.Users.Include(u => u.Office).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? NotFound() : Ok(await ToResponseAsync(user, cancellationToken));
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
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
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
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : NotFound();
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
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
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
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/change-user-class")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ChangeUserClass(int id, ChangeUserClassRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.ChangeUserClassAsync(id, request.UserClass, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : NotFound();
    }

    [HttpPost("{id:int}/block")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Block(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.BlockAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : NotFound();
    }

    [HttpPost("{id:int}/unblock")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Unblock(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.UnblockAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : NotFound();
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var result = await _userService.ResetPasswordAsync(id, cancellationToken);
        return result.Outcome == UserWriteOutcome.Success ? Ok(await ToResponseAsync(result.User!, cancellationToken)) : NotFound();
    }

    private async Task<IActionResult> ToOnboardingResultAsync(UserWriteResult result, CancellationToken cancellationToken) => result.Outcome switch
    {
        UserWriteOutcome.Success => Ok(await ToResponseAsync(result.User!, cancellationToken)),
        UserWriteOutcome.NotFound => NotFound(),
        UserWriteOutcome.InvalidTransition => Problem(title: "This staff record is no longer pending.", statusCode: StatusCodes.Status400BadRequest),
        UserWriteOutcome.ReasonRequired => Problem(title: "A reason is required to decline.", statusCode: StatusCodes.Status400BadRequest),
        UserWriteOutcome.NotAuthorizedToAuthorize => Problem(
            title: "Only a user_class Authorizer of the same user_type as the initiator (or a super admin) can approve/decline this.",
            statusCode: StatusCodes.Status403Forbidden),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private async Task<HashSet<int>> UserIdsForTypeAsync(string userTypeSlug, CancellationToken cancellationToken) =>
        (await _db.RoleUsers.Where(ru => ru.Role.Slug == userTypeSlug).Select(ru => ru.UserId).ToListAsync(cancellationToken)).ToHashSet();

    private async Task<UserResponse> ToResponseAsync(User u, CancellationToken cancellationToken)
    {
        var userType = await _db.RoleUsers
            .Where(ru => ru.UserId == u.Id)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

        return new UserResponse(
            u.Id, u.Email, u.FirstName, u.LastName, u.Phone, u.OfficeId, u.Office?.Name, userType, u.UserClass, u.Blocked,
            u.OnboardingStatus, u.CreatedById, u.OnboardingApprovedById, u.OnboardingApprovedDate,
            u.OnboardingDeclinedById, u.OnboardingDeclinedDate, u.OnboardingDeclinedReason, u.LastLogin);
    }
}
