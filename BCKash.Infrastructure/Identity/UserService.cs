using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Application.Organization;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Auth;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Identity;

/// <summary>Staff onboarding and lifecycle management — see IUserService's and docs/staff-onboarding-rbac-spec.md's doc comments for the maker-checker rule this implements.</summary>
public class UserService : IUserService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UserService> _logger;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly IOfficeScope _scope;
    private readonly StaffPortalSettings _staffPortal;

    /// <summary>Loans still being repaid. A List so EF can translate Contains.</summary>
    private static readonly List<LoanStatus> RunningLoanStatuses = [LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled];

    public UserService(BCKashDbContext db, ICurrentUserContext currentUser, IPasswordHasher passwordHasher, IEmailSender emailSender, ILogger<UserService> logger, ICompanyProfileProvider companyProfile, IOfficeScope scope, IOptions<StaffPortalSettings> staffPortal)
    {
        _staffPortal = staffPortal.Value;
        _scope = scope;
        _companyProfile = companyProfile;
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

            // Typeless (legacy) users keep their old unrestricted behaviour — see IOfficeScope.
            var actingType = await _scope.GetUserTypeAsync(cancellationToken);
            if (actingType is not null && !UserTypeSlugs.Outranks(actingType, userTypeSlug))
            {
                return new UserWriteResult(UserWriteOutcome.UserTypeNotPermitted);
            }

            // Someone who works in a single office onboards staff into it unless they say otherwise.
            var scopeOfficeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
            if (!newUser.OfficeId.HasValue && scopeOfficeIds is { Count: 1 })
            {
                newUser.OfficeId = scopeOfficeIds.First();
            }

            if (!await _scope.CanAccessOfficeAsync(newUser.OfficeId, cancellationToken))
            {
                return new UserWriteResult(UserWriteOutcome.OfficeOutOfScope);
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
        newUser.CreatedAt = DateTime.UtcNow;
        newUser.UpdatedAt = newUser.CreatedAt;

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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
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

        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
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

        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        user.FirstName = updated.FirstName;
        user.LastName = updated.LastName;
        user.Phone = updated.Phone;
        user.Address = updated.Address;
        user.Notes = updated.Notes;
        user.Gender = updated.Gender;

        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> UpdateRecordAsync(int userId, User updated, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == updated.Email.ToLower(), cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.EmailInUse);
        }

        user.Email = updated.Email;
        user.FirstName = updated.FirstName;
        user.LastName = updated.LastName;
        user.Phone = updated.Phone;
        user.Gender = updated.Gender;
        user.Address = updated.Address;
        user.Notes = updated.Notes;
        user.DateOfBirth = updated.DateOfBirth;
        user.NextOfKinName = updated.NextOfKinName;
        user.NextOfKinPhone = updated.NextOfKinPhone;
        user.NextOfKinRelationship = updated.NextOfKinRelationship;
        user.BankName = updated.BankName;
        user.BankAccountNumber = updated.BankAccountNumber;
        user.BankAccountName = updated.BankAccountName;

        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        if (!await _db.Offices.AnyAsync(o => o.Id == officeId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.OfficeNotFound);
        }

        if (!await _scope.CanAccessOfficeAsync(officeId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.OfficeOutOfScope);
        }

        user.OfficeId = officeId;
        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Slug == userTypeSlug, cancellationToken);
        if (role is null || !UserTypeSlugs.All.Contains(userTypeSlug))
        {
            return new UserWriteResult(UserWriteOutcome.UserTypeNotFound);
        }

        var actingType = await _scope.GetUserTypeAsync(cancellationToken);
        if (actingType is not (null or UserTypeSlugs.SuperAdmin) && !UserTypeSlugs.Outranks(actingType, userTypeSlug))
        {
            return new UserWriteResult(UserWriteOutcome.UserTypeNotPermitted);
        }

        // Only directors oversee zones — anyone moved off director loses theirs.
        if (userTypeSlug != UserTypeSlugs.Director)
        {
            _db.UserZones.RemoveRange(await _db.UserZones.Where(uz => uz.UserId == userId).ToListAsync(cancellationToken));
        }

        // A staff member has exactly one user_type — replace any existing type-role
        // assignment(s) rather than adding a second, leaving any non-type role untouched.
        var existingTypeAssignments = await _db.RoleUsers
            .Where(ru => ru.UserId == userId && _db.Roles.Where(r => UserTypeSlugs.All.Contains(r.Slug)).Select(r => r.Id).Contains(ru.RoleId))
            .ToListAsync(cancellationToken);
        _db.RoleUsers.RemoveRange(existingTypeAssignments);

        _db.RoleUsers.Add(new RoleUser { UserId = userId, RoleId = role.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        user.UserClass = userClass;
        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        user.Blocked = true;
        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        user.Blocked = false;
        MarkUpdated(user);
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

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);

        // Same rule as a new account: the emailed temporary password must be replaced on next sign-in.
        user.MustChangePassword = !await IsSuperAdminAsync(user.Id, cancellationToken);
        user.PasswordResetRequestedAt = DateTime.UtcNow;
        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);

        await SendCredentialsEmailAsync(user, temporaryPassword, cancellationToken, isReset: true);

        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> ForcePasswordResetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        if (user.Blocked)
        {
            return new UserWriteResult(UserWriteOutcome.NotActive);
        }

        if (await IsSuperAdminAsync(user.Id, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.SuperAdminExcluded);
        }

        var temporaryPassword = BulkResetPassword();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.PasswordResetRequestedAt = DateTime.UtcNow;
        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);

        await SendCredentialsEmailAsync(user, temporaryPassword, cancellationToken, isReset: true, signInAddress: StaffPortalUrl());

        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> TransferOfficeAsync(int userId, int officeId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.Office).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        if (user.Blocked)
        {
            return new UserWriteResult(UserWriteOutcome.NotActive);
        }

        if (await IsSuperAdminAsync(user.Id, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.SuperAdminExcluded);
        }

        var office = await _db.Offices.FirstOrDefaultAsync(o => o.Id == officeId && o.Active && o.DeletedAt == null, cancellationToken);
        if (office is null)
        {
            return new UserWriteResult(UserWriteOutcome.OfficeNotFound);
        }

        if (!await _scope.CanAccessOfficeAsync(officeId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.OfficeOutOfScope);
        }

        if (user.OfficeId == officeId)
        {
            return new UserWriteResult(UserWriteOutcome.AlreadyInOffice);
        }

        if (await HasRunningLoansAsync(userId, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.HasActiveLoans);
        }

        var previousOfficeName = user.Office?.Name;
        user.OfficeId = officeId;
        user.Office = office;
        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);

        await SendTransferEmailAsync(user, previousOfficeName, office.Name, cancellationToken);

        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> DisableAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        var manageFailure = await CheckCanManageAsync(user, cancellationToken);
        if (manageFailure is not null)
        {
            return new UserWriteResult(manageFailure.Value);
        }

        if (user.Blocked)
        {
            return new UserWriteResult(UserWriteOutcome.NotActive);
        }

        if (await IsSuperAdminAsync(user.Id, cancellationToken))
        {
            return new UserWriteResult(UserWriteOutcome.SuperAdminExcluded);
        }

        user.Blocked = true;
        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> UpdateOwnProfileAsync(User updated, CancellationToken cancellationToken = default)
    {
        var user = _currentUser.UserId.HasValue
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
            : null;
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        user.FirstName = updated.FirstName;
        user.LastName = updated.LastName;
        user.Phone = updated.Phone;
        user.Gender = updated.Gender;
        user.Address = updated.Address;
        user.DateOfBirth = updated.DateOfBirth;
        user.NextOfKinName = updated.NextOfKinName;
        user.NextOfKinPhone = updated.NextOfKinPhone;
        user.NextOfKinRelationship = updated.NextOfKinRelationship;
        user.BankName = updated.BankName;
        user.BankAccountNumber = updated.BankAccountNumber;
        user.BankAccountName = updated.BankAccountName;

        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    public async Task<UserWriteResult> AssignZonesAsync(int userId, IReadOnlyCollection<int> zoneIds, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UserWriteResult(UserWriteOutcome.NotFound);
        }

        if (await GetUserTypeSlugAsync(userId, cancellationToken) != UserTypeSlugs.Director)
        {
            return new UserWriteResult(UserWriteOutcome.NotADirector);
        }

        var distinctZoneIds = zoneIds.Distinct().ToList();
        if (await _db.Zones.CountAsync(z => distinctZoneIds.Contains(z.Id), cancellationToken) != distinctZoneIds.Count)
        {
            return new UserWriteResult(UserWriteOutcome.ZoneNotFound);
        }

        var existing = await _db.UserZones.Where(uz => uz.UserId == userId).ToListAsync(cancellationToken);
        _db.UserZones.RemoveRange(existing.Where(uz => !distinctZoneIds.Contains(uz.ZoneId)));
        foreach (var zoneId in distinctZoneIds.Where(id => existing.All(uz => uz.ZoneId != id)))
        {
            _db.UserZones.Add(new UserZone { UserId = userId, ZoneId = zoneId, AssignedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow });
        }

        MarkUpdated(user);
        await _db.SaveChangesAsync(cancellationToken);
        return new UserWriteResult(UserWriteOutcome.Success, user);
    }

    /// <summary>
    /// Null when the acting user may act on <paramref name="target"/>: always for a super admin (or a
    /// typeless legacy user — see IOfficeScope);
    /// otherwise the target must be in one of their offices (NotFound if not, so out-of-scope staff
    /// stay invisible) and rank below them.
    /// </summary>
    private async Task<UserWriteOutcome?> CheckCanManageAsync(User target, CancellationToken cancellationToken)
    {
        var actingType = await _scope.GetUserTypeAsync(cancellationToken);
        if (actingType is null or UserTypeSlugs.SuperAdmin)
        {
            return null;
        }

        if (!await _scope.CanAccessOfficeAsync(target.OfficeId, cancellationToken))
        {
            return UserWriteOutcome.NotFound;
        }

        return UserTypeSlugs.Outranks(actingType, await GetUserTypeSlugAsync(target.Id, cancellationToken))
            ? null
            : UserWriteOutcome.NotAuthorizedToManage;
    }

    /// <summary>
    /// Records who last changed the staff record and when. Called by the admin actions only — sign-ins
    /// and session changes also save the user row, but they aren't edits to the record.
    /// </summary>
    private void MarkUpdated(User user)
    {
        user.UpdatedById = _currentUser.UserId;
        user.UpdatedAt = DateTime.UtcNow;
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

    /// <summary>
    /// Whether a running loan is tied to the staff member — one they're the loan officer on, or one
    /// belonging to a client or group they look after (including a client's share of a group loan).
    /// </summary>
    private async Task<bool> HasRunningLoansAsync(int userId, CancellationToken cancellationToken)
    {
        var clientIds = _db.Clients.Where(c => c.StaffId == userId && c.DeletedAt == null).Select(c => c.Id);
        var groupIds = _db.Groups.Where(g => g.StaffId == userId).Select(g => g.Id);
        var allocatedLoanIds = _db.GroupLoanAllocations
            .Where(a => a.LoanId != null && a.ClientId != null && clientIds.Contains(a.ClientId.Value))
            .Select(a => a.LoanId!.Value);

        return await _db.Loans.AnyAsync(
            l => l.DeletedAt == null
                && RunningLoanStatuses.Contains(l.Status)
                && (l.LoanOfficerId == userId
                    || (l.ClientId != null && clientIds.Contains(l.ClientId.Value))
                    || (l.GroupId != null && groupIds.Contains(l.GroupId.Value))
                    || allocatedLoanIds.Contains(l.Id)),
            cancellationToken);
    }

    /// <summary>The configured default reset password when it's usable, otherwise a fresh random one of the same length.</summary>
    private string BulkResetPassword()
    {
        var configured = _staffPortal.DefaultResetPassword?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            return TemporaryPasswordGenerator.Generate(StaffPortalSettings.DefaultResetPasswordLength);
        }

        if (configured.Length != StaffPortalSettings.DefaultResetPasswordLength)
        {
            _logger.LogError(
                "StaffPortal:DefaultResetPassword must be exactly {Length} characters; generating a random password instead.",
                StaffPortalSettings.DefaultResetPasswordLength);
            return TemporaryPasswordGenerator.Generate(StaffPortalSettings.DefaultResetPasswordLength);
        }

        return configured;
    }

    private string? StaffPortalUrl() => string.IsNullOrWhiteSpace(_staffPortal.Url) ? null : _staffPortal.Url.Trim();

    private async Task<string?> GetUserTypeSlugAsync(int userId, CancellationToken cancellationToken) =>
        await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary><paramref name="signInAddress"/> overrides the company profile's portal address as the link to sign in at.</summary>
    private async Task SendCredentialsEmailAsync(User user, string temporaryPassword, CancellationToken cancellationToken, bool isReset = false, string? signInAddress = null)
    {
        var company = await _companyProfile.GetAsync(cancellationToken);
        signInAddress ??= company.PortalAddress;
        var subject = isReset ? $"Your {company.Name} password has been reset" : $"Welcome to {company.Name} — your account details";
        var body =
            $"Hello {user.FirstName},\n\n" +
            $"{(isReset ? $"Your {company.Name} portal password has been reset." : $"An account has been created for you on the {company.Name} portal.")}\n\n" +
            $"Email: {user.Email}\n" +
            $"Temporary password: {temporaryPassword}\n\n" +
            (signInAddress is null ? string.Empty : $"Sign in at {signInAddress}\n\n") +
            "Please log in and change this password as soon as possible." +
            company.EmailFooter;

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

    private async Task SendTransferEmailAsync(User user, string? previousOfficeName, string? newOfficeName, CancellationToken cancellationToken)
    {
        var company = await _companyProfile.GetAsync(cancellationToken);
        var portalUrl = StaffPortalUrl();
        var body =
            $"Hello {user.FirstName},\n\n" +
            $"You have been transferred {(previousOfficeName is null ? string.Empty : $"from {previousOfficeName} ")}to {newOfficeName}, effective {DateTime.UtcNow:d MMMM yyyy}.\n\n" +
            "From your next sign-in you'll work with this office's clients, groups and records.\n\n" +
            (portalUrl is null ? string.Empty : $"Sign in at {portalUrl}\n\n") +
            "If you weren't expecting this change, please contact your manager." +
            company.EmailFooter;

        // Best-effort, as with the credentials email — the transfer is already saved.
        try
        {
            await _emailSender.SendAsync(user.Email, $"You have been transferred to {newOfficeName}", body, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to email {Email} about their transfer to {Office}.", user.Email, newOfficeName);
        }
    }
}
