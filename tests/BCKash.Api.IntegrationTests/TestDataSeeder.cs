using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// A valid state/LGA/city/zone combination for creating an office. States and LGAs come from
    /// the startup seed; the city and zone are new on every call so tests don't collide.
    /// </summary>
    public static async Task<OfficeLocation> SeedOfficeLocationAsync(BCKashDbContext db)
    {
        var lga = await db.Lgas.OrderBy(l => l.Id).FirstAsync();
        var city = new City { LgaId = lga.Id, Name = $"Test City {Guid.NewGuid():N}" };
        var zone = new Zone { Name = $"Test Zone {Guid.NewGuid():N}" };
        db.Cities.Add(city);
        db.Zones.Add(zone);
        await db.SaveChangesAsync();

        return new OfficeLocation(lga.StateId, lga.Id, city.Id, zone.Id);
    }

    /// <summary>
    /// A user with a real user_type (the seeded type role and its default permissions), placed in
    /// <paramref name="officeId"/> — the shape office-portal staff have.
    /// </summary>
    public static async Task<User> SeedTypedUserAsync(BCKashDbContext db, string email, string password, string userTypeSlug, int? officeId, UserClass? userClass = null)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = "Test",
            LastName = "User",
            OfficeId = officeId,
            UserClass = userClass,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var role = await db.Roles.SingleAsync(r => r.Slug == userTypeSlug);
        db.RoleUsers.Add(new RoleUser { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>An office in a brand-new zone (or <paramref name="zoneId"/>, when given).</summary>
    public static async Task<Office> SeedOfficeAsync(BCKashDbContext db, int? zoneId = null)
    {
        if (!zoneId.HasValue)
        {
            var zone = new Zone { Name = $"Test Zone {Guid.NewGuid():N}" };
            db.Zones.Add(zone);
            await db.SaveChangesAsync();
            zoneId = zone.Id;
        }

        var office = new Office { Name = $"Test Office {Guid.NewGuid():N}", ZoneId = zoneId, Active = true, CreatedAt = DateTime.UtcNow };
        db.Offices.Add(office);
        await db.SaveChangesAsync();
        return office;
    }

    /// <summary>What a client needs before a controller can approve them: 2 guarantors, 1 reference and an enrolled face.</summary>
    public static async Task SeedApprovalRequirementsAsync(BCKashDbContext db, int clientId)
    {
        (await db.Clients.FindAsync(clientId))!.BiometricEnrolledAt = DateTime.UtcNow;
        foreach (var (kind, name) in new[] { (ClientContact.GuarantorKind, "Chidi Obi"), (ClientContact.GuarantorKind, "Bola Ade"), (ClientContact.ReferenceKind, "Emeka Eze") })
        {
            db.ClientContacts.Add(new ClientContact
            {
                ClientId = clientId,
                Kind = kind,
                FullName = name,
                Phone = "08031234567",
                Address = "12 Marina, Lagos",
                Relationship = "Friend",
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Makes a loan behave like one disbursed before client savings began — every repayment goes wholly to
    /// the loan — for tests about repayment allocation rather than savings.
    /// </summary>
    public static async Task WithoutSavingsAsync(BCKashWebApplicationFactory factory, int loanId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var loan = await db.Loans.SingleAsync(l => l.Id == loanId);
        loan.SavingsRate = null;
        await db.SaveChangesAsync();
    }

    public static async Task MakeSuperAdminAsync(BCKashDbContext db, User user)
    {
        var superAdminRole = await db.Roles.SingleAsync(r => r.Slug == UserTypeSlugs.SuperAdmin);
        db.RoleUsers.Add(new RoleUser { UserId = user.Id, RoleId = superAdminRole.Id });
        await db.SaveChangesAsync();
    }
}

public record OfficeLocation(int StateId, int LgaId, int CityId, int ZoneId);
