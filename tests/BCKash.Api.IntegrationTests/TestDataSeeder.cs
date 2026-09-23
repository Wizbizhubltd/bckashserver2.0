using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;

namespace BCKash.Api.IntegrationTests;

public static class TestDataSeeder
{
    public static async Task<User> SeedUserAsync(
        BCKashDbContext db,
        string email,
        string password,
        bool enableGoogle2fa = false,
        string? google2faSecret = null,
        string? permissionSlug = null,
        IEnumerable<string>? additionalPermissionSlugs = null)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EnableGoogle2fa = enableGoogle2fa,
            Google2faSecret = google2faSecret,
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var slugs = new[] { permissionSlug }.Concat(additionalPermissionSlugs ?? []).Where(s => s is not null).Select(s => s!).Distinct();

        foreach (var slug in slugs)
        {
            var permission = new Permission { Name = slug, Slug = slug };
            db.Permissions.Add(permission);
            await db.SaveChangesAsync();

            var role = new Role { Slug = $"role-{Guid.NewGuid():N}", Name = "Test Role" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.RoleUsers.Add(new RoleUser { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        return user;
    }
}
