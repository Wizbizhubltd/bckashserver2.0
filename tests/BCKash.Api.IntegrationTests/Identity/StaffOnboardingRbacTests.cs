using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Communications;
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

        // --- Super admin onboards a director-Initiator, a director-Authorizer, and a
        //     controller-Authorizer (to prove cross-user_type rejection) — all auto-approved
        //     since the creator is a super admin. ---
        var directorInitiator = await CreateUserAsync(superAdminClient, "director-initiator@bckash.test", UserTypeSlugs.Director, UserClass.Initiator);
        Assert.Equal(UserOnboardingStatus.Approved, directorInitiator.OnboardingStatus);

        var directorAuthorizer = await CreateUserAsync(superAdminClient, "director-authorizer@bckash.test", UserTypeSlugs.Director, UserClass.Authorizer);
        var controllerAuthorizer = await CreateUserAsync(superAdminClient, "controller-authorizer@bckash.test", UserTypeSlugs.Controller, UserClass.Authorizer);

        // --- The director-Initiator logs in and initiates a new hire — NOT auto-approved,
        //     since the creator this time is an ordinary Initiator, not a super admin. ---
        var directorInitiatorPassword = ExtractPassword(recordingSender, "director-initiator@bckash.test");
        var directorInitiatorClient = await LoginAsync(factory, "director-initiator@bckash.test", directorInitiatorPassword);

        var newHireResponse = await directorInitiatorClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            "new-hire@bckash.test", "New", "Hire", null, null, UserTypeSlugs.Director, UserClass.Initiator, null, null, null));
        Assert.True(newHireResponse.IsSuccessStatusCode);
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

        // --- Super admin can assign staff to an office (existing office CRUD + new assign-office action). ---
        var office = await (await superAdminClient.PostAsJsonAsync("/api/v1/offices", new { Name = "HQ" })).Content.ReadFromJsonAsync<OfficeResponse>(TestJson.Options);
        var assignResponse = await superAdminClient.PostAsJsonAsync($"/api/v1/users/{newHire.Id}/assign-office", new AssignOfficeRequest(office!.Id));
        Assert.True(assignResponse.IsSuccessStatusCode);
        var assigned = await assignResponse.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(office.Id, assigned!.OfficeId);
    }

    private static async Task<UserResponse> CreateUserAsync(HttpClient actingClient, string email, string userTypeSlug, UserClass userClass)
    {
        var response = await actingClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            email, "Test", "User", null, null, userTypeSlug, userClass, null, null, null));
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

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(factory, client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private record OfficeResponse(int Id);
}
