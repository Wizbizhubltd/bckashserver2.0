using BCKash.Application.Auth;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Auth;

public class PermissionService : IPermissionService
{
    private readonly BCKashDbContext _db;

    public PermissionService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionSlugsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rolePermissionSlugs = _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .SelectMany(ru => ru.Role.RolePermissions)
            .Select(rp => rp.Permission.Slug);

        var directPermissionSlugs = _db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Slug);

        var slugs = await rolePermissionSlugs.Concat(directPermissionSlugs)
            .Where(slug => slug != null)
            .Distinct()
            .ToListAsync(cancellationToken);

        return slugs.Select(s => s!).ToArray();
    }
}
