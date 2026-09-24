using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

public class OfficeLocationAndZoneTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public OfficeLocationAndZoneTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task All_states_and_lgas_are_seeded()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "locations-seeded@bckash.test", "organization.manage");

        var states = await client.GetFromJsonAsync<List<StateResponse>>("/api/v1/locations/states");
        var lgas = await client.GetFromJsonAsync<List<LgaResponse>>("/api/v1/locations/lgas");
        var lagos = states!.Single(s => s.Name == "Lagos");
        var lagosLgas = await client.GetFromJsonAsync<List<LgaResponse>>($"/api/v1/locations/lgas?stateId={lagos.Id}");

        Assert.Equal(37, states!.Count);
        Assert.Equal(774, lgas!.Count);
        Assert.Equal(20, lagosLgas!.Count);
    }

    [Fact]
    public async Task A_new_office_gets_a_unique_code_and_records_who_created_it_and_when()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-code@bckash.test", "organization.manage");
        var location = await SeedLocationAsync();
        var before = DateTime.UtcNow.AddSeconds(-5);

        var first = await CreateOfficeAsync(client, "Code Branch 1", location);
        var second = await CreateOfficeAsync(client, "Code Branch 2", location);

        Assert.Matches(new Regex(@"^BCK\d{3}-\d{3}-\d{2}$"), first.OfficeCode!);
        Assert.NotEqual(first.OfficeCode, second.OfficeCode);
        Assert.Equal("Test User", first.CreatedByName);
        Assert.True(first.CreatedAt >= before);
        Assert.Equal(location.ZoneId, first.ZoneId);
        Assert.NotNull(first.StateName);
        Assert.NotNull(first.LgaName);
        Assert.NotNull(first.CityName);
        Assert.NotNull(first.ZoneName);
    }

    [Fact]
    public async Task An_office_needs_a_consistent_state_lga_city_and_zone()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-location-rules@bckash.test", "organization.manage");
        var location = await SeedLocationAsync();
        int otherStateId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            otherStateId = await db.States.Where(s => s.Id != location.StateId).Select(s => s.Id).FirstAsync();
        }

        var missing = await client.PostAsJsonAsync("/api/v1/offices", Request("No Location", location) with { ZoneId = null });
        var wrongState = await client.PostAsJsonAsync("/api/v1/offices", Request("Wrong State", location) with { StateId = otherStateId });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongState.StatusCode);
    }

    [Fact]
    public async Task Offices_can_be_filtered_by_type_and_location()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-filters@bckash.test", "organization.manage");
        var zoneA = await SeedLocationAsync();
        var zoneB = await SeedLocationAsync();

        var head = await CreateOfficeAsync(client, "Filter Head", zoneA, defaultOffice: true);
        var branchA = await CreateOfficeAsync(client, "Filter Branch A", zoneA);
        var branchB = await CreateOfficeAsync(client, "Filter Branch B", zoneB);

        var inZoneA = await client.GetFromJsonAsync<List<OfficeResponse>>($"/api/v1/offices?zoneId={zoneA.ZoneId}");
        var branchesInZoneA = await client.GetFromJsonAsync<List<OfficeResponse>>($"/api/v1/offices?zoneId={zoneA.ZoneId}&type=branch");
        var inCityB = await client.GetFromJsonAsync<List<OfficeResponse>>($"/api/v1/offices?cityId={zoneB.CityId}");
        var heads = await client.GetFromJsonAsync<List<OfficeResponse>>("/api/v1/offices?type=head");

        Assert.Equal([head.Id, branchA.Id], inZoneA!.Select(o => o.Id));
        Assert.Equal([branchA.Id], branchesInZoneA!.Select(o => o.Id));
        Assert.Equal([branchB.Id], inCityB!.Select(o => o.Id));
        Assert.All(heads!, o => Assert.True(o.DefaultOffice));
        Assert.Contains(heads!, o => o.Id == head.Id);
    }

    [Fact]
    public async Task Only_super_admins_can_create_or_edit_zones_and_cities()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "zones-staff@bckash.test", "organization.manage");
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "zones-super@bckash.test");
        var location = await SeedLocationAsync();

        var staffZone = await staff.PostAsJsonAsync("/api/v1/zones", new SaveZoneRequest("Staff Zone", null));
        var staffCity = await staff.PostAsJsonAsync("/api/v1/locations/cities", new SaveCityRequest(location.LgaId, "Staff City"));
        Assert.Equal(HttpStatusCode.Forbidden, staffZone.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffCity.StatusCode);

        var created = await superAdmin.PostAsJsonAsync("/api/v1/zones", new SaveZoneRequest("North Zone", "Northern branches"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var zone = await created.Content.ReadFromJsonAsync<ZoneResponse>();

        var renamed = await superAdmin.PutAsJsonAsync($"/api/v1/zones/{zone!.Id}", new SaveZoneRequest("North-East Zone", null));
        Assert.Equal("North-East Zone", (await renamed.Content.ReadFromJsonAsync<ZoneResponse>())!.Name);

        var duplicate = await superAdmin.PostAsJsonAsync("/api/v1/zones", new SaveZoneRequest("North-East Zone", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var city = await superAdmin.PostAsJsonAsync("/api/v1/locations/cities", new SaveCityRequest(location.LgaId, "Admin City"));
        var duplicateCity = await superAdmin.PostAsJsonAsync("/api/v1/locations/cities", new SaveCityRequest(location.LgaId, "Admin City"));
        Assert.Equal(HttpStatusCode.Created, city.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCity.StatusCode);

        // Anyone signed in can read them, for dropdowns and filters.
        var zones = await staff.GetFromJsonAsync<List<ZoneResponse>>("/api/v1/zones");
        Assert.Contains(zones!, z => z.Id == zone.Id);
    }

    [Fact]
    public async Task A_zone_whose_offices_have_staff_cannot_be_deleted()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "zones-delete@bckash.test");
        var location = await SeedLocationAsync();
        var office = await CreateOfficeAsync(superAdmin, "Staffed Branch", location);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var staffMember = await TestDataSeeder.SeedUserAsync(db, "zones-delete-staff@bckash.test", "Correct-Password1!");
            staffMember.OfficeId = office.Id;
            await db.SaveChangesAsync();
        }

        var zone = await superAdmin.GetFromJsonAsync<ZoneResponse>($"/api/v1/zones/{location.ZoneId}");
        Assert.Equal(1, zone!.StaffCount);
        var blocked = await superAdmin.DeleteAsync($"/api/v1/zones/{location.ZoneId}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var staffMember = await db.Users.SingleAsync(u => u.Email == "zones-delete-staff@bckash.test");
            staffMember.OfficeId = null;
            await db.SaveChangesAsync();
        }

        var deleted = await superAdmin.DeleteAsync($"/api/v1/zones/{location.ZoneId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var officeAfter = await superAdmin.GetFromJsonAsync<OfficeResponse>($"/api/v1/offices/{office.Id}");
        Assert.Null(officeAfter!.ZoneId);
    }

    [Fact]
    public async Task A_city_used_by_an_office_cannot_be_deleted()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "cities-delete@bckash.test");
        var location = await SeedLocationAsync();
        await CreateOfficeAsync(superAdmin, "City Branch", location);

        var response = await superAdmin.DeleteAsync($"/api/v1/locations/cities/{location.CityId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<OfficeLocation> SeedLocationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await TestDataSeeder.SeedOfficeLocationAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>());
    }

    private static SaveOfficeRequest Request(string name, OfficeLocation location, bool defaultOffice = false) => new(
        name, null, null, null, null, null, null, null, null, defaultOffice,
        location.StateId, location.LgaId, location.CityId, location.ZoneId);

    private static async Task<OfficeResponse> CreateOfficeAsync(HttpClient client, string name, OfficeLocation location, bool defaultOffice = false)
    {
        var response = await client.PostAsJsonAsync("/api/v1/offices", Request(name, location, defaultOffice));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OfficeResponse>())!;
    }
}
