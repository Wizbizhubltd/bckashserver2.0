using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// Onboarding and editing from the control portal: a super admin onboards a client or group into an
/// office and hands it to a marketer there, who then documents it; a super admin's edits to any client
/// or group skip the approval lock and are recorded in the audit trail.
/// </summary>
public class SuperAdminOnboardingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public SuperAdminOnboardingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Super_admin_must_name_an_office_and_one_of_its_marketers()
    {
        var (officeId, marketerId, _, _, superAdmin) = await SetUpAsync();
        var (_, otherMarketerId, _, _, _) = await SetUpAsync();
        var bvn = NewBvn();
        var check = await CheckAsync(superAdmin, bvn, "Ada Obi", null);
        var client = new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, null, null);

        Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(null, client, marketerId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(officeId, client))).StatusCode);

        // A marketer from another office can't be handed the client.
        Assert.Equal(HttpStatusCode.BadRequest, (await superAdmin.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(officeId, client, otherMarketerId))).StatusCode);

        var response = await superAdmin.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(officeId, client, marketerId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var clientId = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;

        var saved = await superAdmin.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal(officeId, saved!.OfficeId);
        Assert.Equal(marketerId, saved.StaffId);
        Assert.Equal(ClientStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task The_assigned_marketer_documents_a_client_a_super_admin_onboarded()
    {
        var (officeId, marketerId, marketer, _, superAdmin) = await SetUpAsync();
        var bvn = NewBvn();
        var check = await CheckAsync(superAdmin, bvn, "Ada Obi", null);
        var clientId = await OnboardAsync(superAdmin, officeId, marketerId, new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, null, null));

        var asMarketer = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.True(asMarketer!.Actions!.CanEditDetails);
        var nextOfKin = new { FirstName = "Chidi", LastName = "Obi", RelationshipId = (int?)null };
        Assert.True((await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/next-of-kin", nextOfKin)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Super_admin_edits_an_approved_client_without_an_edit_privilege_and_the_change_is_audited()
    {
        var (officeId, marketerId, marketer, controller, superAdmin) = await SetUpAsync();
        var bvn = NewBvn();
        var check = await CheckAsync(superAdmin, bvn, "Ada Obi", null);
        var clientId = await OnboardAsync(superAdmin, officeId, marketerId, new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, null, null));
        using (var scope = _factory.Services.CreateScope())
        {
            await TestDataSeeder.SeedApprovalRequirementsAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), clientId);
        }

        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        var update = new UpdateClientRequest(
            bvn, null, officeId, marketerId, null, null, null, "Ada", null, "Obi-Okafor", "Ada Obi-Okafor", null, "Ada Obi-Okafor", null,
            "08031234567", null, null, null, ClientType.Individual, null, null, null, null, null, null, null, null, null, null, null, null, null);

        // The approved client is locked for the marketer…
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", update)).StatusCode);

        // …but not for a super admin, and the client stays approved.
        var edited = await superAdmin.PutAsJsonAsync($"/api/v1/clients/{clientId}", update);
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        var saved = await superAdmin.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal("Obi-Okafor", saved!.LastName);
        Assert.Equal(ClientStatus.Active, saved.Status);

        var audit = await superAdmin.GetFromJsonAsync<List<ClientAuditEntryResponse>>($"/api/v1/clients/{clientId}/audit", TestJson.Options);
        Assert.Contains(audit!, e => e.Action == "Update" && e.Notes!.Contains("Obi-Okafor"));
    }

    [Fact]
    public async Task Super_admin_onboards_a_group_for_a_marketer_and_group_edits_are_audited()
    {
        var (officeId, marketerId, _, _, superAdmin) = await SetUpAsync();
        var members = new List<OnboardClientRequest>();
        foreach (var name in new[] { "Ada Obi", "Bola Ade", "Chika Eze" })
        {
            var bvn = NewBvn();
            var check = await CheckAsync(superAdmin, bvn, name, null);
            members.Add(new OnboardClientRequest(name, null, null, bvn, check.VerificationId, null, null));
        }

        var response = await superAdmin.PostAsJsonAsync("/api/v1/onboarding/groups", new OnboardGroupRequest(
            officeId, new OnboardGroupDetailsRequest("Unity Traders", null, null, "12 Market Road"), members, marketerId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var onboarded = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!;
        var groupId = onboarded.GroupId!.Value;

        var group = await superAdmin.GetFromJsonAsync<GroupResponse>($"/api/v1/groups/{groupId}", TestJson.Options);
        Assert.Equal(marketerId, group!.StaffId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var memberIds = onboarded.Clients.Select(c => c.Id).ToList();
            Assert.All(await db.Clients.Where(c => memberIds.Contains(c.Id)).ToListAsync(), c => Assert.Equal(marketerId, c.StaffId));
        }

        var edited = await superAdmin.PutAsJsonAsync($"/api/v1/groups/{groupId}", new UpdateGroupRequest(
            officeId, "Unity Market Traders", null, marketerId, group.JoinedDate, null, null, null, null, null, null, null, "12 Market Road", null));
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());

        var audit = await superAdmin.GetFromJsonAsync<List<ClientAuditEntryResponse>>($"/api/v1/groups/{groupId}/audit", TestJson.Options);
        Assert.Contains(audit!, e => e.Action == "Update" && e.Notes!.Contains("Unity Market Traders"));
        Assert.Contains(audit!, e => e.Action == "Created");
    }

    private async Task<int> OnboardAsync(HttpClient superAdmin, int officeId, int marketerId, OnboardClientRequest client)
    {
        var response = await superAdmin.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(officeId, client, marketerId));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;
    }

    private async Task<(int OfficeId, int MarketerId, HttpClient Marketer, HttpClient Controller, HttpClient SuperAdmin)> SetUpAsync()
    {
        int officeId;
        int marketerId;
        var marketerEmail = $"sa-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"sa-controller-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            marketerId = (await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
        }

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"sa-admin-{Guid.NewGuid():N}@bckash.test");
        return (officeId, marketerId, await SignInAsync(marketerEmail), await SignInAsync(controllerEmail), superAdmin);
    }

    private async Task<BvnCheckResponse> CheckAsync(HttpClient client, string bvn, string fullName, string? phone)
    {
        var response = await client.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(bvn, fullName, phone));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BvnCheckResponse>(TestJson.Options))!;
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    /// <summary>As in ClientOnboardingTests: a leading 2–8 so the fake provider finds it; ending in 1 so it matches.</summary>
    private static string NewBvn() => $"{Random.Shared.Next(2, 9)}{Random.Shared.NextInt64(100_000_000, 999_999_999)}1";
}
