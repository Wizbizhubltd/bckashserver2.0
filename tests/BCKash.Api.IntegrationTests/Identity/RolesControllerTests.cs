using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>Role → permission pairs, managed by super admins.</summary>
public class RolesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public RolesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Only_super_admins_can_manage_role_permissions()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "roles-staff@bckash.test", "users.manage");

        var response = await staff.GetAsync("/api/v1/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lists_the_staff_roles_and_every_grantable_permission()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "roles-list@bckash.test");

        var roles = await admin.GetFromJsonAsync<List<RoleResponse>>("/api/v1/roles", TestJson.Options);
        var permissions = await admin.GetFromJsonAsync<List<PermissionResponse>>("/api/v1/roles/permissions", TestJson.Options);

        Assert.Equal(UserTypeSlugs.All, roles!.Select(r => r.Slug));
        var superAdmin = roles!.Single(r => r.Slug == UserTypeSlugs.SuperAdmin);
        Assert.True(superAdmin.Locked);
        Assert.Equal(PermissionCatalog.All.Count, superAdmin.Permissions.Count);
        Assert.Equal(PermissionCatalog.All.Select(p => p.Slug), permissions!.Select(p => p.Slug));
        Assert.All(permissions!, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public async Task A_permission_can_be_added_to_and_removed_from_a_role_and_is_audited()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "roles-edit@bckash.test");
        var marketer = (await admin.GetFromJsonAsync<List<RoleResponse>>("/api/v1/roles", TestJson.Options))!.Single(r => r.Slug == UserTypeSlugs.Marketer);
        Assert.DoesNotContain("reports.view", (await RemoveAsync(admin, marketer.Id, "reports.view")).Permissions);

        var added = await admin.PostAsync($"/api/v1/roles/{marketer.Id}/permissions/reports.view", null);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Contains("reports.view", (await added.Content.ReadFromJsonAsync<RoleResponse>(TestJson.Options))!.Permissions);

        using var scope = _factory.Services.CreateScope();
        var audits = await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().AuditTrail
            .Where(a => a.Module == "Role" && a.Notes!.Contains("marketer") && a.Notes.Contains("reports.view"))
            .CountAsync();
        Assert.True(audits >= 2);
    }

    [Fact]
    public async Task The_super_admin_role_cannot_be_changed()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "roles-locked@bckash.test");
        var superAdmin = (await admin.GetFromJsonAsync<List<RoleResponse>>("/api/v1/roles", TestJson.Options))!.Single(r => r.Slug == UserTypeSlugs.SuperAdmin);

        var response = await admin.DeleteAsync($"/api/v1/roles/{superAdmin.Id}/permissions/users.manage");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_or_legacy_permissions_cannot_be_granted()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "roles-unknown@bckash.test");
        var manager = (await admin.GetFromJsonAsync<List<RoleResponse>>("/api/v1/roles", TestJson.Options))!.Single(r => r.Slug == UserTypeSlugs.Manager);

        var response = await admin.PostAsync($"/api/v1/roles/{manager.Id}/permissions/clients.notes.create", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<RoleResponse> RemoveAsync(HttpClient client, int roleId, string slug)
    {
        var response = await client.DeleteAsync($"/api/v1/roles/{roleId}/permissions/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoleResponse>(TestJson.Options))!;
    }
}
