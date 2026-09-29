using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// Face biometrics against the fake Rekognition (<see cref="FakeFaceBiometrics"/>): enrolling a
/// client's face (which becomes their profile picture), the liveness pass mark, the lock once the
/// client is approved, and the face match every disbursement needs — 90% or better.
/// </summary>
public class ClientBiometricsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public ClientBiometricsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FakeFaceBiometrics Faces => _factory.Services.GetRequiredService<FakeFaceBiometrics>();

    [Fact]
    public async Task Enrolling_a_live_face_makes_it_the_profile_picture_and_a_spoof_is_rejected()
    {
        var (clientId, marketer, _, _) = await SetUpAsync();

        var before = await marketer.GetFromJsonAsync<ClientBiometricsResponse>($"/api/v1/clients/{clientId}/biometrics", TestJson.Options);
        Assert.False(before!.Enrolled);
        Assert.True(before.CanEnroll);
        Assert.Equal(90, before.FaceMatchThreshold);

        // Not convincingly live (a photo held to the camera, say): rejected, nothing enrolled.
        var spoof = await CaptureAsync(marketer, clientId, "enrollment", null, face: $"spoof-{clientId}", confidence: 35);
        Assert.Equal(ClientBiometric.FailedStatus, spoof.Status);
        Assert.Contains("live person", spoof.FailureReason);
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.GetAsync($"/api/v1/clients/{clientId}/photo")).StatusCode);

        var enrolled = await CaptureAsync(marketer, clientId, "enrollment", null, face: $"face-{clientId}");
        Assert.Equal(ClientBiometric.PassedStatus, enrolled.Status);

        var after = await marketer.GetFromJsonAsync<ClientBiometricsResponse>($"/api/v1/clients/{clientId}/biometrics", TestJson.Options);
        Assert.True(after!.Enrolled);
        Assert.Equal(2, after.Captures.Count);
        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.True(client!.HasPhoto);
        Assert.Equal(HttpStatusCode.OK, (await marketer.GetAsync($"/api/v1/clients/{clientId}/photo")).StatusCode);

        // Editing the client's details (the form never sends a picture) keeps the enrolled face.
        var edited = await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", new { client.OfficeId, FirstName = "Ada", LastName = "Obi", DisplayName = "Ada Obi", Address = "34 Ribble Road" });
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        Assert.True((await edited.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options))!.HasPhoto);
        Assert.Equal(HttpStatusCode.OK, (await marketer.GetAsync($"/api/v1/clients/{clientId}/photo")).StatusCode);
    }

    [Fact]
    public async Task Once_approved_the_enrolled_face_is_locked()
    {
        var (clientId, marketer, controller, _) = await SetUpAsync();
        await CaptureAsync(marketer, clientId, "enrollment", null, face: $"face-{clientId}");
        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        var biometrics = await marketer.GetFromJsonAsync<ClientBiometricsResponse>($"/api/v1/clients/{clientId}/biometrics", TestJson.Options);
        Assert.False(biometrics!.CanEnroll);
        var again = await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("enrollment", null));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_loan_is_disbursed_only_after_the_client_passes_a_face_match()
    {
        var (clientId, marketer, _, manager) = await SetUpAsync();
        int loanId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = await db.Clients.FindAsync(clientId);
            var loan = new Loan { ClientId = clientId, OfficeId = client!.OfficeId, ClientType = LoanClientType.Client, Status = LoanStatus.Pending, AppliedAmount = 50_000 };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            loanId = loan.Id;
        }

        var disburse = new DisburseLoanRequest(null, 50_000, null);

        // No enrolled face yet: nothing to compare against, and no disbursement.
        var notEnrolled = await manager.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("loan", loanId));
        Assert.Equal(HttpStatusCode.Conflict, notEnrolled.StatusCode);
        var blocked = await manager.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", disburse);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("Face match needed", await blocked.Content.ReadAsStringAsync());

        // Super admins disburse from the control portal, behind the same face gate.
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"sa-disburse-{clientId}@bckash.test");
        var superAdminBlocked = await superAdmin.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", disburse);
        Assert.Equal(HttpStatusCode.Conflict, superAdminBlocked.StatusCode);
        Assert.Contains("Face match needed", await superAdminBlocked.Content.ReadAsStringAsync());

        await CaptureAsync(marketer, clientId, "enrollment", null, face: $"face-{clientId}");

        // Only staff who disburse loans run the loan face match.
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest("loan", loanId))).StatusCode);

        // Someone else in front of the camera: well under 90%, rejected.
        var impostor = await CaptureAsync(manager, clientId, "loan", loanId, face: $"impostor-{clientId}");
        Assert.Equal(ClientBiometric.FailedStatus, impostor.Status);
        Assert.Equal(12m, impostor.Similarity);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", disburse)).StatusCode);

        var match = await CaptureAsync(manager, clientId, "loan", loanId, face: $"face-{clientId}");
        Assert.Equal(ClientBiometric.PassedStatus, match.Status);
        Assert.True(match.Similarity >= 90);

        var checks = await manager.GetFromJsonAsync<List<LoanFaceCheckResponse>>($"/api/v1/loans/{loanId}/face-checks", TestJson.Options);
        Assert.True(Assert.Single(checks!).Verified);

        // The face gate is cleared; this seeded loan has no product, so the disbursement itself stops later.
        var cleared = await manager.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", disburse);
        Assert.DoesNotContain("Face match needed", await cleared.Content.ReadAsStringAsync());
        var superAdminCleared = await superAdmin.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", disburse);
        Assert.DoesNotContain("Face match needed", await superAdminCleared.Content.ReadAsStringAsync());
        Assert.NotEqual(HttpStatusCode.Forbidden, superAdminCleared.StatusCode);
    }

    [Fact]
    public async Task Browser_credentials_are_only_for_staff_who_capture_faces()
    {
        var (_, marketer, _, _) = await SetUpAsync();
        var response = await marketer.PostAsync("/api/v1/biometrics/credentials", content: null);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("ASIAFAKE", (await response.Content.ReadFromJsonAsync<LivenessCredentialsResponse>(TestJson.Options))!.AccessKeyId);

        var noPermission = await AuthenticatedClientFactory.CreateAsync(_factory, $"no-faces-{Guid.NewGuid():N}@bckash.test", "reports.view");
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.PostAsync("/api/v1/biometrics/credentials", content: null)).StatusCode);
    }

    [Theory]
    [InlineData("Ada Obi - biometric enrollment.jpg", "img/Ada-Obi-biometric-enrollment-")]
    [InlineData("Ada Obi - utility bill.pdf", "documents/Ada-Obi-utility-bill-")]
    [InlineData("statement.XLSX", "documents/statement-")]
    public void S3_keys_put_images_under_img_and_everything_else_under_documents_named_after_the_file(string suggested, string expectedPrefix)
    {
        var key = S3FileStorageService.KeyFor(suggested, new DateTime(2026, 9, 28, 15, 30, 0, DateTimeKind.Utc));
        Assert.StartsWith(expectedPrefix + "20260928153000-", key);
        Assert.EndsWith(Path.GetExtension(suggested).ToLowerInvariant(), key);
    }

    private async Task<FaceCaptureResponse> CaptureAsync(HttpClient staff, int clientId, string purpose, int? loanId, string face, float confidence = 99)
    {
        // Each test class has its own host (and fake), and a class's tests run one at a time.
        Faces.NextFace = face;
        Faces.NextConfidence = confidence;
        var started = await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/biometrics/sessions", new StartFaceCaptureRequest(purpose, loanId));

        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        var session = await started.Content.ReadFromJsonAsync<FaceCaptureSessionResponse>(TestJson.Options);
        Assert.Equal("eu-west-1", session!.Region);

        var completed = await staff.PostAsync($"/api/v1/clients/{clientId}/biometrics/sessions/{session.SessionId}/complete", content: null);
        Assert.True(completed.IsSuccessStatusCode, await completed.Content.ReadAsStringAsync());
        return (await completed.Content.ReadFromJsonAsync<FaceCaptureResponse>(TestJson.Options))!;
    }

    /// <summary>An office with a marketer, a controller and a manager, and a client the marketer onboarded.</summary>
    private async Task<(int ClientId, HttpClient Marketer, HttpClient Controller, HttpClient Manager)> SetUpAsync()
    {
        var marketerEmail = $"bio-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"bio-controller-{Guid.NewGuid():N}@bckash.test";
        var managerEmail = $"bio-manager-{Guid.NewGuid():N}@bckash.test";
        int clientId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            var marketer = await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, managerEmail, Password, UserTypeSlugs.Manager, officeId);

            var client = new Client { FirstName = "Ada", LastName = "Obi", DisplayName = "Ada Obi", OfficeId = officeId, Status = ClientStatus.Pending, CreatedById = marketer.Id, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
            await TestDataSeeder.SeedApprovalRequirementsAsync(db, clientId);
        }

        return (clientId, await SignInAsync(marketerEmail), await SignInAsync(controllerEmail), await SignInAsync(managerEmail));
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
