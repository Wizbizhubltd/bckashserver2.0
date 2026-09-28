using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Application.Organization;
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
/// 1. Every permission in PermissionCatalog — the ones controllers actually check (a Permission
///    row must exist for a role to be granted it — see PermissionPolicyProvider), with its name
///    and description kept up to date.
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
    private static readonly string[] AllKnownPermissionSlugs = PermissionCatalog.All.Select(p => p.Slug).ToArray();

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

        foreach (var definition in PermissionCatalog.All)
        {
            if (!existing.TryGetValue(definition.Slug, out var permission))
            {
                permission = new Permission { Slug = definition.Slug };
                db.Permissions.Add(permission);
                existing[definition.Slug] = permission;
            }

            // Keep the readable name and description in step with the catalogue.
            permission.Name = definition.Name;
            permission.Description = definition.Description;
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
            var granted = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync(cancellationToken);

            // Super admin always holds every permission — including ones added to the catalogue
            // after it was first seeded. Its permissions can't be edited (see RolesController).
            if (roleSlug == UserTypeSlugs.SuperAdmin)
            {
                foreach (var permission in permissionsBySlug.Where(p => PermissionCatalog.Slugs.Contains(p.Key)).Select(p => p.Value))
                {
                    if (!granted.Contains(permission.Id))
                    {
                        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                    }
                }

                continue;
            }

            if (granted.Count > 0)
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
        var companyProfile = services.GetRequiredService<ICompanyProfileProvider>();
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
            var company = await companyProfile.GetAsync(cancellationToken);
            await emailSender.SendAsync(
                superAdmin.Email,
                $"Your {company.Name} super admin account",
                $"Hello {superAdmin.FirstName},\n\n" +
                $"A super admin account has been created for you on the {company.Name} portal.\n\n" +
                $"Email: {superAdmin.Email}\n" +
                $"Temporary password: {temporaryPassword}\n\n" +
                "Please log in and change this password immediately." +
                company.EmailFooter,
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
