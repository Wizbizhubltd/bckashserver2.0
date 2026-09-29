using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>
/// Settings → Loan → who may run the face match before disbursement. Its own fixture: saving the
/// setting changes the rule for every test sharing the database.
/// </summary>
public class LoanFaceMatchRolesTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public LoanFaceMatchRolesTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FakeFaceBiometrics Faces => _factory.Services.GetRequiredService<FakeFaceBiometrics>();

    [Fact]
    public async Task Only_the_ticked_roles_may_run_the_face_match()
    {
        var (clientId, loanId, marketer, controller, manager) = await SetUpAsync();
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "face-roles-sa@bckash.test");
        await CaptureAsync(marketer, clientId, "enrollment", null, $"face-{clientId}");

        // Until the setting is saved, anyone who services loans may: the manager can, the marketer can't.
        Assert.True(await CanVerifyAsync(manager, loanId));
        Assert.False(await CanVerifyAsync(marketer, loanId));

        var saved = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(LoanFaceMatchRoles.SettingKey, "controller,super_admin"));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);

        Assert.False(await CanVerifyAsync(manager, loanId));
        var refused = await manager.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("loan", loanId));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        Assert.True(await CanVerifyAsync(controller, loanId));
        Assert.True(await CanVerifyAsync(superAdmin, loanId));
        var match = await CaptureAsync(superAdmin, clientId, "loan", loanId, $"face-{clientId}");
        Assert.Equal(ClientBiometric.PassedStatus, match.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("controller,teller")]
    public async Task The_setting_must_name_known_roles(string value)
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"face-roles-invalid-{Guid.NewGuid():N}@bckash.test");

        var response = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(LoanFaceMatchRoles.SettingKey, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<bool> CanVerifyAsync(HttpClient staff, int loanId) =>
        Assert.Single((await staff.GetFromJsonAsync<List<LoanFaceCheckResponse>>($"/api/v1/loans/{loanId}/face-checks", TestJson.Options))!).CanVerify;

    private async Task<FaceCaptureResponse> CaptureAsync(HttpClient staff, int clientId, string purpose, int? loanId, string face)
    {
        Faces.NextFace = face;
        Faces.NextConfidence = 99;
        var started = await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest(purpose, loanId));
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        var session = await started.Content.ReadFromJsonAsync<FaceCaptureSessionResponse>(TestJson.Options);

        var completed = await staff.PostAsync($"/api/v1/clients/{clientId}/biometrics/sessions/{session!.SessionId}/complete", content: null);
        Assert.True(completed.IsSuccessStatusCode, await completed.Content.ReadAsStringAsync());
        return (await completed.Content.ReadFromJsonAsync<FaceCaptureResponse>(TestJson.Options))!;
    }

    /// <summary>An office with a marketer, a controller and a manager, a client the marketer onboarded, and a pending loan for them.</summary>
    private async Task<(int ClientId, int LoanId, HttpClient Marketer, HttpClient Controller, HttpClient Manager)> SetUpAsync()
    {
        var marketerEmail = $"face-roles-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"face-roles-controller-{Guid.NewGuid():N}@bckash.test";
        var managerEmail = $"face-roles-manager-{Guid.NewGuid():N}@bckash.test";
        int clientId, loanId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            var marketer = await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, managerEmail, Password, UserTypeSlugs.Manager, officeId);

            var client = new Client { FirstName = "Ngozi", LastName = "Eze", DisplayName = "Ngozi Eze", OfficeId = officeId, Status = ClientStatus.Pending, CreatedById = marketer.Id, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            var loan = new Loan { ClientId = client.Id, OfficeId = officeId, ClientType = LoanClientType.Client, Status = LoanStatus.Pending, AppliedAmount = 50_000 };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            (clientId, loanId) = (client.Id, loan.Id);
        }

        return (clientId, loanId, await SignInAsync(marketerEmail), await SignInAsync(controllerEmail), await SignInAsync(managerEmail));
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
