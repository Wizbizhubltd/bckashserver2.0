using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Auth;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Identity;

/// <summary>Staff onboarding and lifecycle management — see IUserService's and docs/staff-onboarding-rbac-spec.md's doc comments for the maker-checker rule this implements.</summary>
public class UserService : IUserService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UserService> _logger;

    public UserService(BCKashDbContext db, ICurrentUserContext currentUser, IPasswordHasher passwordHasher, IEmailSender emailSender, ILogger<UserService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<UserWriteResult> CreateAsync(User newUser, string userTypeSlug, CancellationToken cancellationToken = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Slug == userTypeSlug, cancellationToken);
        if (role is null || !UserTypeSlugs.All.Contains(userTypeSlug))
        {
            return new UserWriteResult(UserWriteOutcome.UserTypeNotFound);
        }

        var actingUserId = _currentUser.UserId;
        var actingIsSuperAdmin = await IsSuperAdminAsync(actingUserId, cancellationToken);

        if (!actingIsSuperAdmin)
        {
            var actingUser = actingUserId.HasValue ? await _db.Users.FindAsync([actingUserId.Value], cancellationToken) : null;
            if (!UserOnboardingRules.CanInitiate(actingUser?.UserClass))
            {
                return new UserWriteResult(UserWriteOutcome.NotAuthorizedToInitiate);
            }
        }

        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == newUser.Email.ToLower(), cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.EmailInUse);
        }

        if (newUser.OfficeId.HasValue && !await _db.Offices.AnyAsync(o => o.Id == newUser.OfficeId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.OfficeNotFound);
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        newUser.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        newUser.CreatedById = actingUserId;

        // Everyone but a super admin must replace the emailed temporary password on first sign-in.
        newUser.MustChangePassword = userTypeSlug != UserTypeSlugs.SuperAdmin;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (actingIsSuperAdmin)
        {
            newUser.OnboardingStatus = UserOnboardingStatus.Approved;
            newUser.OnboardingApprovedById = actingUserId;
            newUser.OnboardingApprovedDate = today;
        }
        else
        {
            newUser.OnboardingStatus = UserOnboardingStatus.Pending;
        }

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(cancellationToken);

        _db.RoleUsers.Add(new RoleUser { UserId = newUser.Id, RoleId = role.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);

        await SendCredentialsEmailAsync(newUser, temporaryPassword, cancellationToken);

        return new UserWriteResult(UserWriteOutcome.Success, newUser);
    }

    public async Task<UserWriteResult> ApproveOnboardingAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        if (!UserOnboardingRules.CanTransition(user.OnboardingStatus))
        {
            return new UserWriteResult(UserWriteOutcome.InvalidTransition);
        }

        var authorizationFailure = await CheckAuthorizerAsync(user, cancellationToken);
        if (authorizationFailure is not null)
        {
            return new UserWriteResult(authorizationFailure.Value);
        }

        user.OnboardingStatus = UserOnboardingStatus.Approved;
        user.OnboardingApprovedById = _currentUser.UserId;
        user.OnboardingApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> DeclineOnboardingAsync(int userId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new UserWriteResult(UserWriteOutcome.ReasonRequired);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        if (!UserOnboardingRules.CanTransition(user.OnboardingStatus))
        {
            return new UserWriteResult(UserWriteOutcome.InvalidTransition);
        }

        var authorizationFailure = await CheckAuthorizerAsync(user, cancellationToken);
        if (authorizationFailure is not null)
        {
            return new UserWriteResult(authorizationFailure.Value);
        }

        user.OnboardingStatus = UserOnboardingStatus.Declined;
        user.OnboardingDeclinedById = _currentUser.UserId;
        user.OnboardingDeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        user.OnboardingDeclinedReason = reason;
        user.Blocked = true; // a declined staff record must never be able to log in

        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> UpdateAsync(int userId, User updated, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        user.FirstName = updated.FirstName;
        user.LastName = updated.LastName;
        user.Phone = updated.Phone;
        user.Address = updated.Address;
        user.Notes = updated.Notes;
        user.Gender = updated.Gender;

        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> AssignOfficeAsync(int userId, int officeId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        if (!await _db.Offices.AnyAsync(o => o.Id == officeId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.OfficeNotFound);
        }

        user.OfficeId = officeId;
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> ChangeUserTypeAsync(int userId, string userTypeSlug, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Slug == userTypeSlug, cancellationToken);
        if (role is null || !UserTypeSlugs.All.Contains(userTypeSlug))
        {
            return new UserWriteResult(UserWriteOutcome.UserTypeNotFound);
        }

        // A staff member has exactly one user_type — replace any existing type-role
        // assignment(s) rather than adding a second, leaving any non-type role untouched.
        var existingTypeAssignments = await _db.RoleUsers
            .Where(ru => ru.UserId == userId && _db.Roles.Where(r => UserTypeSlugs.All.Contains(r.Slug)).Select(r => r.Id).Contains(ru.RoleId))
            .ToListAsync(cancellationToken);
        _db.RoleUsers.RemoveRange(existingTypeAssignments);

        _db.RoleUsers.Add(new RoleUser { UserId = userId, RoleId = role.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);

        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> ChangeUserClassAsync(int userId, UserClass userClass, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        user.UserClass = userClass;
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> BlockAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        user.Blocked = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> UnblockAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        user.Blocked = false;
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);

        // Same rule as a new account: the emailed temporary password must be replaced on next sign-in.
        user.MustChangePassword = !await IsSuperAdminAsync(user.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await SendCredentialsEmailAsync(user, temporaryPassword, cancellationToken, isReset: true);

        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    /// <summary>Null means authorized (either a super admin, or a valid same-user_type Authorizer); otherwise the specific failure to return.</summary>
    private async Task<UserWriteOutcome?> CheckAuthorizerAsync(User targetUser, CancellationToken cancellationToken)
    {
        var actingUserId = _currentUser.UserId;
        if (await IsSuperAdminAsync(actingUserId, cancellationToken))
        {
            return null;
        }

        var actingUser = actingUserId.HasValue ? await _db.Users.FindAsync([actingUserId.Value], cancellationToken) : null;
        var initiatorType = targetUser.CreatedById.HasValue ? await GetUserTypeSlugAsync(targetUser.CreatedById.Value, cancellationToken) : null;
        var actingType = actingUserId.HasValue ? await GetUserTypeSlugAsync(actingUserId.Value, cancellationToken) : null;

        return UserOnboardingRules.CanAuthorize(actingUser?.UserClass, actingType, initiatorType)
            ? null
            : UserWriteOutcome.NotAuthorizedToAuthorize;
    }

    private async Task<bool> IsSuperAdminAsync(int? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return false;
        }

        var type = await GetUserTypeSlugAsync(userId.Value, cancellationToken);
        return type == UserTypeSlugs.SuperAdmin;
    }

    private async Task<string?> GetUserTypeSlugAsync(int userId, CancellationToken cancellationToken) =>
        await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task SendCredentialsEmailAsync(User user, string temporaryPassword, CancellationToken cancellationToken, bool isReset = false)
    {
        var subject = isReset ? "Your BCKash password has been reset" : "Welcome to BCKash — your account details";
        var body =
            $"Hello {user.FirstName},\n\n" +
            $"{(isReset ? "Your BCKash portal password has been reset." : "An account has been created for you on the BCKash portal.")}\n\n" +
            $"Email: {user.Email}\n" +
            $"Temporary password: {temporaryPassword}\n\n" +
            "Please log in and change this password as soon as possible.";

        // Best-effort — a staff record is already committed by the time this runs; a
        // transient email failure must not turn an otherwise-successful create/reset into an
        // error response for the caller (same principle as IdentityBootstrapSeeder's fix for
        // the equivalent bootstrap case, which crashed the whole app on an SMTP failure).
        try
        {
            await _emailSender.SendAsync(user.Email, subject, body, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to email {Email}'s temporary password. Use this to log in, then change it immediately: {TemporaryPassword}",
                user.Email,
                temporaryPassword);
        }
    }
}
