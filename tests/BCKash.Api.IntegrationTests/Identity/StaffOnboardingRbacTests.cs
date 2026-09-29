using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>
/// End-to-end proof of the whole staff-onboarding RBAC rule described in
/// docs/staff-onboarding-rbac-spec.md: the first super admin is seeded (no manual SQL needed),
/// a non-super-admin Initiator's new staff record starts Pending, an Authorizer of a
/// *different* user_type is rejected, an Authorizer of the *same* user_type approves it, and
/// the newly-approved staff member can log in with the password that was emailed to them.
/// </summary>
public class StaffOnboardingRbacTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _baseFactory;

    public StaffOnboardingRbacTests(BCKashWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task Bootstrap_super_admin_then_full_initiator_authorizer_flow()
    {
        const string superAdminEmail = "superadmin@bootstrap.test";
        var factory = _baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Bootstrap:SuperAdminEmail", superAdminEmail));
        var recordingSender = (RecordingEmailSender)factory.Services.GetRequiredService<IEmailSender>();

        // --- The seeded super admin can log in immediately, no manual SQL required ---
        var superAdminPassword = ExtractPassword(recordingSender, superAdminEmail);
        var superAdminClient = await LoginAsync(factory, superAdminEmail, superAdminPassword);

        var me = await superAdminClient.GetFromJsonAsync<UserResponse>("/api/v1/users/me", TestJson.Options);
        Assert.Equal(UserTypeSlugs.SuperAdmin, me!.UserType);
        Assert.Equal(UserOnboardingStatus.Approved, me.OnboardingStatus);

        // --- Super admin sets up an office in a zone, to onboard staff into. ---
        OfficeLocation location;
        using (var scope = factory.Services.CreateScope())
        {
            location = await TestDataSeeder.SeedOfficeLocationAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>());
        }

        var officeRequest = new { Name = "HQ", location.StateId, location.LgaId, location.CityId, location.ZoneId };
        var office = await (await superAdminClient.PostAsJsonAsync("/api/v1/offices", officeRequest)).Content.ReadFromJsonAsync<OfficeResponse>(TestJson.Options);

        // --- Super admin onboards a director-Initiator, a director-Authorizer, and a
        //     controller-Authorizer (to prove cross-user_type rejection) — all auto-approved
        //     since the creator is a super admin. Directors oversee zones, which only a super admin assigns. ---
        var directorInitiator = await CreateUserAsync(superAdminClient, "director-initiator@bckash.test", UserTypeSlugs.Director, UserClass.Initiator);
        Assert.Equal(UserOnboardingStatus.Approved, directorInitiator.OnboardingStatus);

        var directorAuthorizer = await CreateUserAsync(superAdminClient, "director-authorizer@bckash.test", UserTypeSlugs.Director, UserClass.Authorizer);
        var controllerAuthorizer = await CreateUserAsync(superAdminClient, "controller-authorizer@bckash.test", UserTypeSlugs.Controller, UserClass.Authorizer, office!.Id);

        foreach (var director in new[] { directorInitiator, directorAuthorizer })
        {
            var zonesResponse = await superAdminClient.PutAsJsonAsync($"/api/v1/users/{director.Id}/zones", new AssignZonesRequest([location.ZoneId]));
            Assert.True(zonesResponse.IsSuccessStatusCode, await zonesResponse.Content.ReadAsStringAsync());
            var withZones = await zonesResponse.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
            Assert.Equal([location.ZoneId], withZones!.Zones.Select(z => z.Id));
        }

        // Zones are for directors only.
        var controllerZones = await superAdminClient.PutAsJsonAsync($"/api/v1/users/{controllerAuthorizer.Id}/zones", new AssignZonesRequest([location.ZoneId]));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, controllerZones.StatusCode);

        // --- The director-Initiator logs in and initiates a new hire — NOT auto-approved,
        //     since the creator this time is an ordinary Initiator, not a super admin. ---
        var directorInitiatorPassword = ExtractPassword(recordingSender, "director-initiator@bckash.test");
        var directorInitiatorClient = await LoginAsync(factory, "director-initiator@bckash.test", directorInitiatorPassword);

        // A director can't onboard a fellow director — only staff ranked below them.
        var peerResponse = await directorInitiatorClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            "peer-director@bckash.test", "Peer", "Director", null, office.Id, UserTypeSlugs.Director, UserClass.Initiator, null, null, null));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, peerResponse.StatusCode);

        var newHireResponse = await directorInitiatorClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            "new-hire@bckash.test", "New", "Hire", null, office.Id, UserTypeSlugs.Manager, UserClass.Initiator, null, null, null));
        Assert.True(newHireResponse.IsSuccessStatusCode, await newHireResponse.Content.ReadAsStringAsync());
        var newHire = await newHireResponse.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(UserOnboardingStatus.Pending, newHire!.OnboardingStatus);

        // A newly-initiated (Pending) staff member cannot log in yet.
        var loginBeforeApproval = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("new-hire@bckash.test", ExtractPassword(recordingSender, "new-hire@bckash.test")));
        Assert.False(loginBeforeApproval.IsSuccessStatusCode);

        // --- A controller-Authorizer (different user_type) is rejected. ---
        var controllerAuthorizerClient = await LoginAsync(factory, "controller-authorizer@bckash.test", ExtractPassword(recordingSender, "controller-authorizer@bckash.test"));
        var rejectedApproval = await controllerAuthorizerClient.PostAsync($"/api/v1/users/{newHire.Id}/approve-onboarding", content: null);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, rejectedApproval.StatusCode);

        // --- A director-Authorizer (same user_type as the initiator) succeeds. ---
        var directorAuthorizerClient = await LoginAsync(factory, "director-authorizer@bckash.test", ExtractPassword(recordingSender, "director-authorizer@bckash.test"));
        var approval = await directorAuthorizerClient.PostAsync($"/api/v1/users/{newHire.Id}/approve-onboarding", content: null);
        Assert.True(approval.IsSuccessStatusCode);
        var approved = await approval.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(UserOnboardingStatus.Approved, approved!.OnboardingStatus);

        // The newly-approved staff member can now log in with the password emailed at creation.
        var newHireClient = await LoginAsync(factory, "new-hire@bckash.test", ExtractPassword(recordingSender, "new-hire@bckash.test"));
        var newHireMe = await newHireClient.GetFromJsonAsync<UserResponse>("/api/v1/users/me", TestJson.Options);
        Assert.Equal("new-hire@bckash.test", newHireMe!.Email);
        Assert.Equal(office.Id, newHireMe.OfficeId);

        // --- Super admin can move staff to another office (assign-office action). ---
        var secondOffice = await (await superAdminClient.PostAsJsonAsync("/api/v1/offices", officeRequest with { Name = "Branch" })).Content.ReadFromJsonAsync<OfficeResponse>(TestJson.Options);
        var assignResponse = await superAdminClient.PostAsJsonAsync($"/api/v1/users/{newHire.Id}/assign-office", new AssignOfficeRequest(secondOffice!.Id));
        Assert.True(assignResponse.IsSuccessStatusCode);
        var assigned = await assignResponse.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(secondOffice.Id, assigned!.OfficeId);
    }

    private static async Task<UserResponse> CreateUserAsync(HttpClient actingClient, string email, string userTypeSlug, UserClass userClass, int? officeId = null)
    {
        var response = await actingClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            email, "Test", "User", null, officeId, userTypeSlug, userClass, null, null, null));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options))!;
    }

    private static string ExtractPassword(RecordingEmailSender sender, string toAddress)
    {
        var email = sender.Sent.Last(e => e.ToAddress == toAddress);
        var match = Regex.Match(email.Body, @"Temporary password: (\S+)");
        Assert.True(match.Success, $"No temporary password found in email to {toAddress}: {email.Body}");
        return match.Groups[1].Value;
    }

    // Staff onboarded by someone else start with a temporary password they must replace before the
    // API lets them do anything else — the bootstrapped super admin is the only one exempt.
    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(factory, client, email, password);
        var accessToken = tokens.AccessToken;

        Assert.Equal(email != "superadmin@bootstrap.test", tokens.UserData.MustChangePassword);
        if (tokens.UserData.MustChangePassword)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var changeResponse = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(password, "Changed-Password1!"));
            Assert.True(changeResponse.IsSuccessStatusCode, await changeResponse.Content.ReadAsStringAsync());
            accessToken = (await changeResponse.Content.ReadFromJsonAsync<ChangePasswordResponse>())!.AccessToken;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private record OfficeResponse(int Id);
}
