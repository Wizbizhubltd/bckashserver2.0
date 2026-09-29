using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// Settings → Loan → "Face capture mandatory" switched off: a client with no face capture can still be
/// approved and apply for a loan. (Its own class, so the setting never leaks into other tests.)
/// </summary>
public class FaceCaptureOptionalTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public FaceCaptureOptionalTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task With_face_capture_optional_a_client_without_one_is_approved_and_can_apply_for_a_loan()
    {
        var marketerEmail = $"face-optional-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"face-optional-controller-{Guid.NewGuid():N}@bckash.test";
        int clientId, officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            var marketer = await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
            var client = new Client { FirstName = "Tolu", LastName = "Ade", DisplayName = "Tolu Ade", OfficeId = officeId, Status = ClientStatus.Pending, CreatedById = marketer.Id, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
            await TestDataSeeder.SeedApprovalRequirementsAsync(db, clientId);
            client.BiometricEnrolledAt = null;
            db.Settings.Add(new Setting { SettingKey = FaceCaptureRules.SettingKey, SettingValue = "0" });
            await db.SaveChangesAsync();
        }

        var marketerHttp = await SignInAsync(marketerEmail);
        var controllerHttp = await SignInAsync(controllerEmail);

        Assert.Empty((await controllerHttp.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.ApprovalBlockers!);
        Assert.True((await controllerHttp.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        var detail = await marketerHttp.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Null(detail!.BiometricEnrolledAt);
        Assert.Null(detail.LoanBlocker);

        // Past the approval and face checks; this made-up product then stops the application.
        var application = await marketerHttp.PostAsJsonAsync("/api/v1/loan-applications", new { ClientType = "Client", ClientId = clientId, OfficeId = officeId, LoanProductId = 999_999, Amount = 50_000 });
        Assert.NotEqual(HttpStatusCode.Conflict, application.StatusCode);
    }

    [Fact]
    public void Only_an_explicit_off_makes_face_capture_optional()
    {
        Assert.True(FaceCaptureRules.IsRequired(null));
        Assert.True(FaceCaptureRules.IsRequired("1"));
        Assert.False(FaceCaptureRules.IsRequired("0"));
        Assert.NotNull(FaceCaptureRules.Validate(FaceCaptureRules.SettingKey, "maybe"));
        Assert.Null(FaceCaptureRules.Validate(FaceCaptureRules.SettingKey, "0"));
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
