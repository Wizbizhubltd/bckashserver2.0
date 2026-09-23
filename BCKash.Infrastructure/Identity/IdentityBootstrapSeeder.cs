using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Auth;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Identity;

/// <summary>
/// Seeds the RBAC scaffolding the new user_type/user_class model needs, and the very first
/// super admin — idempotent (NFR-10), safe to run on every boot:
/// 1. Every permission slug any controller currently checks for (a Permission row must exist
///    for a role to be granted it — see BCKash.Api/Authorization/PermissionPolicyProvider.cs).
/// 2. The five user_type roles (super_admin/controller/director/manager/marketer — see
///    UserTypeSlugs), each wired to a starting permission set. This is a reasonable default,
///    not a business-confirmed mapping — adjust via ordinary RolePermission rows at any time,
///    no code change needed.
/// 3. The first super_admin user, ONLY if `Bootstrap:SuperAdminEmail` is configured AND no
///    super_admin exists yet — closes the "how do I get my first login" bootstrap gap
///    permanently (see docs/staff-onboarding-rbac-spec.md). Left opt-in (not automatic) so a
///    fresh dev/test database doesn't silently gain an admin nobody configured.
/// </summary>
public class IdentityBootstrapSeeder : IHostedService
{
    private static readonly string[] AllKnownPermissionSlugs =
    [
        "organization.manage", "clients.manage", "groups.manage",
        "loan-products.manage", "loan-applications.manage", "loan-applications.approve", "loan-servicing.manage",
        "savings-products.manage", "savings-accounts.manage",
        "gl.manage", "gl.closure-reopen", "settings.manage",
        "assets.manage",
        "expenses.manage", "expenses.approve", "expense-budgets.manage", "expense-budgets.approve",
        "other-income.manage", "other-income.approve",
        "payroll.manage", "payroll.run",
        "campaigns.manage", "campaigns.run",
        "reports.view", "report-schedules.manage",
        "users.manage",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions = new Dictionary<string, string[]>
    {
        [UserTypeSlugs.SuperAdmin] = AllKnownPermissionSlugs,
        [UserTypeSlugs.Controller] =
        [
            "organization.manage", "users.manage", "gl.manage", "gl.closure-reopen", "settings.manage",
            "reports.view", "report-schedules.manage", "loan-applications.approve",
            "expenses.approve", "expense-budgets.approve", "other-income.approve", "payroll.manage",
        ],
        [UserTypeSlugs.Director] =
        [
            "organization.manage", "users.manage", "reports.view", "report-schedules.manage",
            "loan-applications.approve", "expenses.approve", "expense-budgets.approve", "other-income.approve",
            "gl.manage", "gl.closure-reopen", "settings.manage", "campaigns.manage",
        ],
        [UserTypeSlugs.Manager] =
        [
            "clients.manage", "groups.manage", "loan-products.manage", "loan-applications.manage", "loan-servicing.manage",
            "savings-products.manage", "savings-accounts.manage", "assets.manage",
            "expenses.manage", "expense-budgets.manage", "other-income.manage", "payroll.run",
            "campaigns.manage", "campaigns.run", "reports.view",
        ],
        [UserTypeSlugs.Marketer] = ["clients.manage", "groups.manage", "loan-applications.manage", "campaigns.run", "reports.view"],
    };

    private readonly IServiceProvider _serviceProvider;

    public IdentityBootstrapSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();

        var permissionsBySlug = await SeedPermissionsAsync(db, cancellationToken);
        var rolesBySlug = await SeedUserTypeRolesAsync(db, cancellationToken);
        await SeedRolePermissionsAsync(db, rolesBySlug, permissionsBySlug, cancellationToken);
        await SeedSuperAdminAsync(scope.ServiceProvider, db, rolesBySlug, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<Dictionary<string, Permission>> SeedPermissionsAsync(BCKashDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.Where(p => p.Slug != null).ToDictionaryAsync(p => p.Slug!, cancellationToken);

        foreach (var slug in AllKnownPermissionSlugs)
        {
            if (existing.ContainsKey(slug))
            {
                continue;
            }

            var permission = new Permission { Name = slug, Slug = slug };
            db.Permissions.Add(permission);
            existing[slug] = permission;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static async Task<Dictionary<string, Role>> SeedUserTypeRolesAsync(BCKashDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Where(r => UserTypeSlugs.All.Contains(r.Slug)).ToDictionaryAsync(r => r.Slug, cancellationToken);

        foreach (var slug in UserTypeSlugs.All)
        {
            if (existing.ContainsKey(slug))
            {
                continue;
            }

            var role = new Role { Slug = slug, Name = ToDisplayName(slug), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Roles.Add(role);
            existing[slug] = role;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    /// <summary>Only fills in permissions for a role that currently has none — an operator who has already customized a role's permissions via the API is never overwritten on restart.</summary>
    private static async Task SeedRolePermissionsAsync(
        BCKashDbContext db, Dictionary<string, Role> rolesBySlug, Dictionary<string, Permission> permissionsBySlug, CancellationToken cancellationToken)
    {
        foreach (var (roleSlug, permissionSlugs) in DefaultRolePermissions)
        {
            var role = rolesBySlug[roleSlug];
            var hasAny = await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id, cancellationToken);
            if (hasAny)
            {
                continue;
            }

            foreach (var permissionSlug in permissionSlugs)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionsBySlug[permissionSlug].Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSuperAdminAsync(
        IServiceProvider services, BCKashDbContext db, Dictionary<string, Role> rolesBySlug, CancellationToken cancellationToken)
    {
        var settings = services.GetRequiredService<IOptions<IdentityBootstrapSettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.SuperAdminEmail))
        {
            return;
        }

        var superAdminRole = rolesBySlug[UserTypeSlugs.SuperAdmin];
        var alreadyHasSuperAdmin = await db.RoleUsers.AnyAsync(ru => ru.RoleId == superAdminRole.Id, cancellationToken);
        if (alreadyHasSuperAdmin)
        {
            return;
        }

        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var emailSender = services.GetRequiredService<IEmailSender>();
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var superAdmin = new User
        {
            Email = settings.SuperAdminEmail,
            FirstName = settings.SuperAdminFirstName,
            LastName = settings.SuperAdminLastName,
            Phone = settings.SuperAdminPhone,
            PasswordHash = passwordHasher.Hash(temporaryPassword),
            OnboardingStatus = UserOnboardingStatus.Approved,
            // A super admin bypasses the maker-checker rules in UserOnboardingRules entirely, but
            // still needs a UserClass value — Authorizer is the top of that hierarchy.
            UserClass = UserClass.Authorizer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(superAdmin);
        await db.SaveChangesAsync(cancellationToken);

        superAdmin.OnboardingApprovedById = superAdmin.Id; // self-approved — there's no one else yet
        superAdmin.OnboardingApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        db.RoleUsers.Add(new RoleUser { UserId = superAdmin.Id, RoleId = superAdminRole.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);

        var logger = services.GetRequiredService<ILogger<IdentityBootstrapSeeder>>();

        // The super admin account is already committed above — email delivery is best-effort
        // notification, not part of that transaction. A failure here (a bad SMTP config, the
        // relay being briefly unreachable, etc.) must never crash application startup, which
        // would otherwise take the whole API down over a non-critical courtesy email — a real
        // bug caught during development. On failure, the temporary password is logged instead,
        // so the account is still reachable.
        try
        {
            await emailSender.SendAsync(
                superAdmin.Email,
                "Your BCKash super admin account",
                $"Hello {superAdmin.FirstName},\n\n" +
                "A super admin account has been created for you on the BCKash portal.\n\n" +
                $"Email: {superAdmin.Email}\n" +
                $"Temporary password: {temporaryPassword}\n\n" +
                "Please log in and change this password immediately.",
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to email the seeded super admin's temporary password to {Email}. " +
                "Use this temporary password to log in, then change it immediately: {TemporaryPassword}",
                superAdmin.Email,
                temporaryPassword);
        }
    }

    private static string ToDisplayName(string slug) =>
        string.Join(' ', slug.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
