using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>
/// Portal sign-in rules (super admins on the control portal only, everyone else on the office
/// portal only), office/zone scoping of data, staff seniority, and the self-service profile.
/// </summary>
public class OfficePortalAccessTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public OfficePortalAccessTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(UserTypeSlugs.SuperAdmin, PortalAccessRules.ControlPortal, true)]
    [InlineData(UserTypeSlugs.SuperAdmin, PortalAccessRules.OfficePortal, false)]
    [InlineData(UserTypeSlugs.Marketer, PortalAccessRules.OfficePortal, true)]
    [InlineData(UserTypeSlugs.Marketer, PortalAccessRules.ControlPortal, false)]
    [InlineData(UserTypeSlugs.Director, PortalAccessRules.OfficePortal, true)]
    [InlineData(UserTypeSlugs.Controller, PortalAccessRules.ControlPortal, false)]
    public async Task Each_user_type_can_only_sign_in_to_its_own_portal(string userType, string portal, bool allowed)
    {
        var email = $"{userType}-{portal}-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            await TestDataSeeder.SeedTypedUserAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), email, Password, userType, officeId: null);
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, portal));

        if (allowed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("wrong_portal", problem.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Marketer_sees_only_their_own_offices_clients()
    {
        int ownClientId, otherClientId;
        string email = $"scoped-marketer-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var ownOffice = await TestDataSeeder.SeedOfficeAsync(db);
            var otherOffice = await TestDataSeeder.SeedOfficeAsync(db);
            await TestDataSeeder.SeedTypedUserAsync(db, email, Password, UserTypeSlugs.Marketer, ownOffice.Id);
            ownClientId = await SeedClientAsync(db, ownOffice.Id);
            otherClientId = await SeedClientAsync(db, otherOffice.Id);
        }

        var client = await SignInAsync(email);

        var list = await client.GetFromJsonAsync<PagedResult<ClientListItemResponse>>("/api/v1/clients?pageSize=100", TestJson.Options);
        Assert.Contains(list!.Items, c => c.Id == ownClientId);
        Assert.DoesNotContain(list.Items, c => c.Id == otherClientId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/clients/{ownClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/clients/{otherClientId}")).StatusCode);

        var offices = await client.GetFromJsonAsync<List<OfficeResponse>>("/api/v1/offices", TestJson.Options);
        Assert.Single(offices!);
    }

    [Fact]
    public async Task Director_sees_every_office_in_their_zones_and_nothing_else()
    {
        int zoneId, zoneOfficeA, zoneOfficeB, outsideOffice, directorId;
        var email = $"zoned-director-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var first = await TestDataSeeder.SeedOfficeAsync(db);
            zoneId = first.ZoneId!.Value;
            zoneOfficeA = first.Id;
            zoneOfficeB = (await TestDataSeeder.SeedOfficeAsync(db, zoneId)).Id;
            outsideOffice = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            directorId = (await TestDataSeeder.SeedTypedUserAsync(db, email, Password, UserTypeSlugs.Director, officeId: null)).Id;
        }

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"zones-admin-{Guid.NewGuid():N}@bckash.test");
        var assign = await superAdmin.PutAsJsonAsync($"/api/v1/users/{directorId}/zones", new AssignZonesRequest([zoneId]));
        Assert.True(assign.IsSuccessStatusCode, await assign.Content.ReadAsStringAsync());

        var director = await SignInAsync(email);
        var officeIds = (await director.GetFromJsonAsync<List<OfficeResponse>>("/api/v1/offices", TestJson.Options))!.Select(o => o.Id).ToList();
        Assert.Equal(new[] { zoneOfficeA, zoneOfficeB }.Order(), officeIds.Order());
        Assert.DoesNotContain(outsideOffice, officeIds);

        var zones = await director.GetFromJsonAsync<List<ZoneResponse>>("/api/v1/zones", TestJson.Options);
        Assert.Equal(zoneId, Assert.Single(zones!).Id);

        // Only a super admin assigns zones.
        var selfAssign = await director.PutAsJsonAsync($"/api/v1/users/{directorId}/zones", new AssignZonesRequest([zoneId]));
        Assert.Equal(HttpStatusCode.Forbidden, selfAssign.StatusCode);
    }

    [Fact]
    public async Task Manager_onboards_marketers_into_their_office_but_not_controllers()
    {
        int officeId;
        var email = $"initiating-manager-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, email, Password, UserTypeSlugs.Manager, officeId, UserClass.Initiator);
        }

        var manager = await SignInAsync(email);

        var controller = await manager.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            $"new-controller-{Guid.NewGuid():N}@bckash.test", "New", "Controller", null, null, UserTypeSlugs.Controller, UserClass.Initiator, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, controller.StatusCode);

        // No office given: a single-office manager onboards into their own office.
        var marketer = await manager.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            $"new-marketer-{Guid.NewGuid():N}@bckash.test", "New", "Marketer", null, null, UserTypeSlugs.Marketer, UserClass.Initiator, null, null, null));
        Assert.True(marketer.IsSuccessStatusCode, await marketer.Content.ReadAsStringAsync());
        var created = await marketer.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(officeId, created!.OfficeId);
        Assert.Equal(UserOnboardingStatus.Pending, created.OnboardingStatus);
    }

    [Fact]
    public async Task Staff_complete_their_own_onboarding_details()
    {
        var email = $"profile-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            await TestDataSeeder.SeedTypedUserAsync(db, email, Password, UserTypeSlugs.Marketer, office.Id);
        }

        var client = await SignInAsync(email);

        var before = await client.GetFromJsonAsync<UserResponse>("/api/v1/users/me", TestJson.Options);
        Assert.False(before!.ProfileComplete);
        Assert.Contains("bankAccountNumber", before.MissingProfileFields);

        var response = await client.PutAsJsonAsync("/api/v1/users/me/profile", new UpdateMyProfileRequest(
            "Ada", "Obi", "08031234567", Gender.Female, "12 Marina, Lagos", new DateOnly(1990, 5, 1),
            "Chidi Obi", "08039876543", "Brother", "Access Bank", "0123456789", "Ada Obi"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var after = await response.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.True(after!.ProfileComplete);
        Assert.Empty(after.MissingProfileFields);
        Assert.Equal("+2348031234567", after.Phone);
        Assert.Equal("+2348039876543", after.NextOfKinPhone);

        var badAccount = await client.PutAsJsonAsync("/api/v1/users/me/profile", new UpdateMyProfileRequest(
            "Ada", "Obi", null, null, null, null, null, null, null, null, "12345", null));
        Assert.Equal(HttpStatusCode.BadRequest, badAccount.StatusCode);
    }

    [Fact]
    public async Task Super_admin_ticks_the_modules_a_role_can_open()
    {
        var email = $"modules-marketer-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            await TestDataSeeder.SeedTypedUserAsync(db, email, Password, UserTypeSlugs.Marketer, (await TestDataSeeder.SeedOfficeAsync(db)).Id);
        }

        var marketer = await SignInAsync(email);
        var me = await marketer.GetFromJsonAsync<UserResponse>("/api/v1/users/me", TestJson.Options);
        Assert.Equal([OfficePortalModules.Clients, OfficePortalModules.Loans], me!.Modules);

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"modules-admin-{Guid.NewGuid():N}@bckash.test");
        var roles = await superAdmin.GetFromJsonAsync<List<RoleResponse>>("/api/v1/roles", TestJson.Options);
        var marketerRole = roles!.Single(r => r.Slug == UserTypeSlugs.Marketer);
        var superAdminRole = roles.Single(r => r.Slug == UserTypeSlugs.SuperAdmin);

        try
        {
            var ticked = await superAdmin.PostAsync($"/api/v1/roles/{marketerRole.Id}/modules/staff", content: null);
            Assert.True(ticked.IsSuccessStatusCode, await ticked.Content.ReadAsStringAsync());
            me = await marketer.GetFromJsonAsync<UserResponse>("/api/v1/users/me", TestJson.Options);
            Assert.Equal([OfficePortalModules.Staff, OfficePortalModules.Clients, OfficePortalModules.Loans], me!.Modules);

            Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.PostAsync($"/api/v1/roles/{marketerRole.Id}/modules/savings", content: null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await superAdmin.PostAsync($"/api/v1/roles/{superAdminRole.Id}/modules/staff", content: null)).StatusCode);

            // Only super admins configure modules.
            Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PostAsync($"/api/v1/roles/{marketerRole.Id}/modules/staff", content: null)).StatusCode);
        }
        finally
        {
            // The marketer role is shared with other tests in this fixture.
            await superAdmin.DeleteAsync($"/api/v1/roles/{marketerRole.Id}/modules/staff");
        }
    }

    [Theory]
    [InlineData(UserTypeSlugs.Marketer, true)]
    [InlineData(UserTypeSlugs.Manager, true)]
    [InlineData(UserTypeSlugs.Controller, false)]
    [InlineData(UserTypeSlugs.Director, false)]
    public async Task Only_managers_and_marketers_can_add_clients(string userType, bool allowed)
    {
        var email = $"client-creator-{userType}-{Guid.NewGuid():N}@bckash.test";
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, email, Password, userType, officeId);
        }

        var client = await SignInAsync(email);
        var bvn = $"1{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}"[..10] + "1";
        var check = await client.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(bvn, "New Client", null));
        Assert.True(check.IsSuccessStatusCode, await check.Content.ReadAsStringAsync());
        var verificationId = (await check.Content.ReadFromJsonAsync<BvnCheckResponse>(TestJson.Options))!.VerificationId;

        var response = await client.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            officeId, new OnboardClientRequest("New Client", null, null, bvn, verificationId, null, null)));

        if (allowed)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // Staff always onboard through the BVN-verified flow; the plain create is closed to them.
        var plain = await client.PostAsJsonAsync("/api/v1/clients", new { OfficeId = officeId, FirstName = "New", LastName = "Client" });
        Assert.Equal(HttpStatusCode.Forbidden, plain.StatusCode);
    }

    [Fact]
    public async Task Super_admins_cannot_add_clients()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"client-super-{Guid.NewGuid():N}@bckash.test");
        var response = await superAdmin.PostAsJsonAsync("/api/v1/clients", new { FirstName = "New", LastName = "Client" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Super_admin_bulk_moves_offices_into_a_zone_and_adds_zones_to_a_director()
    {
        int firstOffice, secondOffice, targetZone, otherZone, keptZone, directorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            firstOffice = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            secondOffice = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            targetZone = (await TestDataSeeder.SeedOfficeAsync(db)).ZoneId!.Value;
            otherZone = (await TestDataSeeder.SeedOfficeAsync(db)).ZoneId!.Value;
            keptZone = (await TestDataSeeder.SeedOfficeAsync(db)).ZoneId!.Value;
            directorId = (await TestDataSeeder.SeedTypedUserAsync(db, $"bulk-director-{Guid.NewGuid():N}@bckash.test", Password, UserTypeSlugs.Director, null)).Id;
        }

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"bulk-admin-{Guid.NewGuid():N}@bckash.test");

        var moved = await superAdmin.PostAsJsonAsync($"/api/v1/zones/{targetZone}/offices", new AssignOfficesToZoneRequest([firstOffice, secondOffice]));
        Assert.True(moved.IsSuccessStatusCode, await moved.Content.ReadAsStringAsync());
        Assert.Equal(3, (await moved.Content.ReadFromJsonAsync<ZoneResponse>(TestJson.Options))!.OfficeCount);
        var offices = await superAdmin.GetFromJsonAsync<List<OfficeResponse>>($"/api/v1/offices?zoneId={targetZone}", TestJson.Options);
        Assert.Contains(offices!, o => o.Id == firstOffice);
        Assert.Contains(offices!, o => o.Id == secondOffice);

        await superAdmin.PutAsJsonAsync($"/api/v1/users/{directorId}/zones", new AssignZonesRequest([keptZone]));
        var added = await superAdmin.PostAsJsonAsync($"/api/v1/users/{directorId}/zones", new AssignZonesRequest([targetZone, otherZone]));
        Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
        var director = await added.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(new[] { keptZone, targetZone, otherZone }.Order(), director!.Zones.Select(z => z.Id).Order());

        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, $"bulk-staff-{Guid.NewGuid():N}@bckash.test", "organization.manage");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/v1/zones/{targetZone}/offices", new AssignOfficesToZoneRequest([firstOffice]))).StatusCode);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task<int> SeedClientAsync(BCKashDbContext db, int officeId)
    {
        var client = new Client { FirstName = "Scoped", LastName = "Client", OfficeId = officeId, Status = ClientStatus.Active, CreatedAt = DateTime.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }
}
