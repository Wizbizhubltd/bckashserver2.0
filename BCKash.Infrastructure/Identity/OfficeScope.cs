using BCKash.Application.Identity;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Identity;

/// <summary>Resolves the caller's office scope once per request (registered Scoped) — see <see cref="IOfficeScope"/>.</summary>
public class OfficeScope : IOfficeScope
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    private bool _resolved;
    private string? _userType;
    private IReadOnlyCollection<int>? _officeIds;

    public OfficeScope(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<string?> GetUserTypeAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(cancellationToken);
        return _userType;
    }

    public async Task<IReadOnlyCollection<int>?> GetOfficeIdsAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(cancellationToken);
        return _officeIds;
    }

    public async Task<bool> CanAccessOfficeAsync(int? officeId, CancellationToken cancellationToken = default)
    {
        var officeIds = await GetOfficeIdsAsync(cancellationToken);
        return officeIds is null || (officeId.HasValue && officeIds.Contains(officeId.Value));
    }

    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            _officeIds = [];
            return;
        }

        _userType = await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

        // A user with no user_type (legacy accounts, API-only clients) keeps the unrestricted access
        // they had before office scoping existed — neither portal lets them sign in.
        if (_userType is null or UserTypeSlugs.SuperAdmin)
        {
            _officeIds = null;
            return;
        }

        if (_userType == UserTypeSlugs.Director)
        {
            var zoneIds = _db.UserZones.Where(uz => uz.UserId == userId).Select(uz => uz.ZoneId);
            _officeIds = await _db.Offices
                .Where(o => o.ZoneId.HasValue && zoneIds.Contains(o.ZoneId.Value))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            return;
        }

        var officeId = await _db.Users.Where(u => u.Id == userId).Select(u => u.OfficeId).FirstOrDefaultAsync(cancellationToken);
        _officeIds = officeId.HasValue ? [officeId.Value] : [];
    }
}
