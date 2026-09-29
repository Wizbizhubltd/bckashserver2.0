using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// Client onboarding from the office portal (BVN check → onboard single or group), the high-risk flag
/// for clients kept on details that differ from their BVN, controller approval (and groups approving
/// once all members are), and deletion — direct for never-approved records, by super-admin-approved
/// request otherwise. The test host uses the fake BVN provider: a BVN ending in 9 comes back mismatched.
/// </summary>
public class ClientOnboardingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public ClientOnboardingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Marketer_onboards_a_single_client_whose_bvn_matches()
    {
        var (officeId, marketer, _) = await SetUpOfficeAsync();
        var bvn = NewBvn();

        var check = await CheckAsync(marketer, bvn, "Ada Obi", "08031234567");
        Assert.True(check.Matches);

        var response = await marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            null, new OnboardClientRequest("Ada Obi", "ada@example.com", "08031234567", bvn, check.VerificationId, null, null)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var onboarded = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single();
        Assert.False(onboarded.IsHighRisk);

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{onboarded.Id}", TestJson.Options);
        Assert.Equal(ClientStatus.Pending, client!.Status);
        Assert.Equal(officeId, client.OfficeId);
        Assert.Equal("+2348031234567", client.Mobile);
        Assert.True(client.Actions!.CanEditDetails);
        Assert.False(client.Actions.CanApprove);

        // The same BVN can't be onboarded twice, and the lookup can't be reused.
        var again = await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(bvn, "Ada Obi", null));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Keeping_client_details_over_a_mismatched_bvn_needs_a_reason_and_flags_high_risk_until_marked_safe()
    {
        var (_, marketer, controller) = await SetUpOfficeAsync();
        var bvn = NewBvn(mismatch: true);
        var check = await CheckAsync(marketer, bvn, "Ada Obi", "08031234567");
        Assert.False(check.Matches);
        Assert.Contains(check.Comparisons, c => c.Field == "Last name" && !c.Matches);

        Task<HttpResponseMessage> Onboard(string? source, string? reason) =>
            marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
                null, new OnboardClientRequest("Ada Obi", null, "08031234567", bvn, check.VerificationId, source, reason)));

        Assert.Equal(HttpStatusCode.BadRequest, (await Onboard(null, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Onboard("client", " ")).StatusCode);

        var response = await Onboard("client", "Married recently; BVN still carries her maiden name.");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var clientId = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;

        await SeedContactsAsync(clientId);
        var client = await controller.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.True(client!.IsHighRisk);
        Assert.Equal("Obi", client.LastName);
        Assert.False(client.Actions!.CanApprove);
        Assert.Equal(HttpStatusCode.Conflict, (await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).StatusCode);

        // Only a super admin clears the flag.
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/mark-safe", new MarkSafeRequest(null))).StatusCode);
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"risk-admin-{Guid.NewGuid():N}@bckash.test");
        Assert.True((await superAdmin.PostAsJsonAsync($"/api/v1/clients/{clientId}/mark-safe", new MarkSafeRequest("Marriage certificate seen."))).IsSuccessStatusCode);

        var approved = await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { });
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());

        var audit = await controller.GetFromJsonAsync<List<ClientAuditEntryResponse>>($"/api/v1/clients/{clientId}/audit", TestJson.Options);
        Assert.Contains(audit!, e => e.Action == "Onboarded (high risk)");
    }

    [Fact]
    public async Task Choosing_the_bvn_details_saves_the_providers_names_without_a_flag()
    {
        var (_, marketer, _) = await SetUpOfficeAsync();
        var bvn = NewBvn(mismatch: true);
        var check = await CheckAsync(marketer, bvn, "Ada Obi", null);

        var response = await marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            null, new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, "bvn", null)));
        var clientId = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal("ADEBAYO", client!.LastName);
        Assert.False(client.IsHighRisk);
    }

    [Fact]
    public async Task Group_needs_three_members_is_approved_once_all_are_and_then_needs_a_request_to_delete()
    {
        var (_, marketer, controller) = await SetUpOfficeAsync();

        var members = new List<OnboardClientRequest>();
        foreach (var name in new[] { "Ada Obi", "Bola Ade", "Chika Eze" })
        {
            var bvn = NewBvn();
            var check = await CheckAsync(marketer, bvn, name, null);
            members.Add(new OnboardClientRequest(name, null, null, bvn, check.VerificationId, null, null));
        }

        var tooSmall = await marketer.PostAsJsonAsync("/api/v1/onboarding/groups", new OnboardGroupRequest(
            null, new OnboardGroupDetailsRequest("Unity Traders", null, null, null), members.Take(2).ToList()));
        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);

        var response = await marketer.PostAsJsonAsync("/api/v1/onboarding/groups", new OnboardGroupRequest(
            null, new OnboardGroupDetailsRequest("Unity Traders", "08031234567", null, "12 Market Road"), members));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var onboarded = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!;
        var groupId = onboarded.GroupId!.Value;

        var summary = await marketer.GetFromJsonAsync<GroupSummaryResponse>($"/api/v1/groups/{groupId}/summary", TestJson.Options);
        Assert.Equal([GroupMemberRoles.Leader, GroupMemberRoles.Assistant, GroupMemberRoles.Organizer], summary!.Members.Select(m => m.Role));
        Assert.Equal(3, summary.PendingMembers);
        Assert.True(summary.CanDelete);

        // Managers and marketers don't approve; controllers do, and the group follows its last member.
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PostAsJsonAsync($"/api/v1/clients/{onboarded.Clients[0].Id}/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await controller.PostAsJsonAsync($"/api/v1/groups/{groupId}/activate", new { })).StatusCode);
        foreach (var member in onboarded.Clients)
        {
            await SeedContactsAsync(member.Id);
            Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{member.Id}/activate", new { })).IsSuccessStatusCode);
        }

        var group = await controller.GetFromJsonAsync<GroupResponse>($"/api/v1/groups/{groupId}", TestJson.Options);
        Assert.Equal(GroupStatus.Active, group!.Status);

        // Approved: no direct delete, for the group or its members — a request with a reason instead.
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.DeleteAsync($"/api/v1/groups/{groupId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.DeleteAsync($"/api/v1/clients/{onboarded.Clients[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await marketer.PostAsJsonAsync($"/api/v1/groups/{groupId}/deletion-requests", new ReasonRequest(" "))).StatusCode);
        var raised = await marketer.PostAsJsonAsync($"/api/v1/groups/{groupId}/deletion-requests", new ReasonRequest("Group disbanded."));
        Assert.Equal(HttpStatusCode.Created, raised.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.PostAsJsonAsync($"/api/v1/groups/{groupId}/deletion-requests", new ReasonRequest("Again."))).StatusCode);

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"deletions-admin-{Guid.NewGuid():N}@bckash.test");
        var pending = await superAdmin.GetFromJsonAsync<PagedResult<DeletionRequestResponse>>("/api/v1/deletion-requests?status=Pending", TestJson.Options);
        var request = pending!.Items.Single(r => r.EntityType == DeletionRequest.GroupEntity && r.EntityId == groupId);
        Assert.Equal("Group disbanded.", request.Reason);

        Assert.True((await superAdmin.PostAsJsonAsync($"/api/v1/deletion-requests/{request.Id}/approve", new ReviewDeletionRequest(null))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.GetAsync($"/api/v1/groups/{groupId}")).StatusCode);

        // Its clients stay on the platform.
        Assert.Equal(HttpStatusCode.OK, (await marketer.GetAsync($"/api/v1/clients/{onboarded.Clients[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task Only_the_onboarding_staff_member_documents_a_client_and_never_approved_clients_delete_directly()
    {
        var (officeId, marketer, controller) = await SetUpOfficeAsync();
        var otherMarketerEmail = $"other-marketer-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            await TestDataSeeder.SeedTypedUserAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), otherMarketerEmail, Password, UserTypeSlugs.Marketer, officeId);
        }

        var otherMarketer = await SignInAsync(otherMarketerEmail);
        var bvn = NewBvn();
        var check = await CheckAsync(marketer, bvn, "Ada Obi", null);
        var clientId = (await (await marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            null, new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, null, null)))).Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;

        var nextOfKin = new { FirstName = "Chidi", LastName = "Obi", RelationshipId = (int?)null };
        Assert.Equal(HttpStatusCode.Forbidden, (await otherMarketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/next-of-kin", nextOfKin)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherMarketer.GetAsync($"/api/v1/clients/{clientId}/next-of-kin")).StatusCode);

        // The face capture is documentation too: only the onboarding marketer runs it. It becomes the profile picture.
        Assert.Equal(HttpStatusCode.Forbidden, (await otherMarketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("enrollment", null))).StatusCode);
        var session = await (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("enrollment", null)))
            .Content.ReadFromJsonAsync<FaceCaptureSessionResponse>(TestJson.Options);
        var enrolled = await marketer.PostAsync($"/api/v1/clients/{clientId}/biometrics/sessions/{session!.SessionId}/complete", content: null);
        Assert.True(enrolled.IsSuccessStatusCode, await enrolled.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await controller.GetAsync($"/api/v1/clients/{clientId}/photo")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherMarketer.DeleteAsync($"/api/v1/clients/{clientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await marketer.DeleteAsync($"/api/v1/clients/{clientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.GetAsync($"/api/v1/clients/{clientId}")).StatusCode);

        // A deleted client's BVN can be onboarded again.
        var recheck = await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(bvn, "Ada Obi", null));
        Assert.Equal(HttpStatusCode.OK, recheck.StatusCode);
    }

    [Fact]
    public async Task Bvn_not_found_and_controllers_cannot_onboard()
    {
        var (_, marketer, controller) = await SetUpOfficeAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest("01234567891", "Ada Obi", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest("123", "Ada Obi", null))).StatusCode);

        var bvn = NewBvn();
        var check = await CheckAsync(controller, bvn, "Ada Obi", null);
        var response = await controller.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            null, new OnboardClientRequest("Ada Obi", null, null, bvn, check.VerificationId, null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedContactsAsync(int clientId)
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedApprovalRequirementsAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), clientId);
    }

    private async Task<(int OfficeId, HttpClient Marketer, HttpClient Controller)> SetUpOfficeAsync()
    {
        int officeId;
        var marketerEmail = $"onboard-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"onboard-controller-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
        }

        return (officeId, await SignInAsync(marketerEmail), await SignInAsync(controllerEmail));
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

    /// <summary>A fresh 11-digit BVN starting 2–8 (the fake provider treats a leading 0 as not found); ending in 9 makes the fake return a mismatch.</summary>
    private static string NewBvn(bool mismatch = false)
    {
        var digits = $"{Random.Shared.Next(2, 9)}{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        return digits + (mismatch ? "9" : "1");
    }
}
