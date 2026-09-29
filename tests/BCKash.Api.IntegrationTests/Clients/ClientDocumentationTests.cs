using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// A client's documents (NIN slip, utility bill, ID card — each with its number, at most 2 MB) and
/// their guarantors and references (at least 2 and 1 before approval).
/// </summary>
public class ClientDocumentationTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public ClientDocumentationTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Nin_slip_needs_an_11_digit_nin_a_picture_or_pdf_and_at_most_2mb_and_replaces_the_last_one()
    {
        var (clientId, marketer, _) = await SetUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, "nin_slip", null, "1234", "nin.pdf", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, "nin_slip", null, "12345678901", "nin.docx", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, "nin_slip", null, "12345678901", "nin.pdf", 2 * 1024 * 1024 + 1)).StatusCode);

        // Office staff always say which document it is.
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, null, null, null, "scan.pdf", 1024)).StatusCode);

        var nin = $"2{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(marketer, clientId, "nin_slip", null, nin, "nin.pdf", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(marketer, clientId, "nin_slip", null, nin, "nin-new.jpg", 2048)).StatusCode);

        var documents = await marketer.GetFromJsonAsync<List<DocumentResponse>>($"/api/v1/clients/{clientId}/documents", TestJson.Options);
        var ninSlip = Assert.Single(documents!, d => d.Category == ClientDocumentRules.NinSlip);
        Assert.Equal("nin-new.jpg", ninSlip.Name);
        Assert.Equal(nin, ninSlip.IdNumber);

        // One NIN, one person.
        var (otherClientId, otherMarketer, _) = await SetUpAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(otherMarketer, otherClientId, "nin_slip", null, nin, "nin.pdf", 1024)).StatusCode);
    }

    [Fact]
    public async Task Utility_bill_and_id_card_carry_their_numbers_and_the_id_its_type()
    {
        var (clientId, marketer, _) = await SetUpAsync();

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(marketer, clientId, "utility_bill", null, "0123456789", "bill.png", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, "id_card", null, "A12345678", "id.jpg", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(marketer, clientId, "id_card", "birth_certificate", "A12345678", "id.jpg", 1024)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(marketer, clientId, "id_card", "international_passport", "A12345678", "passport.jpg", 1024)).StatusCode);

        var documents = await marketer.GetFromJsonAsync<List<DocumentResponse>>($"/api/v1/clients/{clientId}/documents", TestJson.Options);
        var id = Assert.Single(documents!, d => d.Category == ClientDocumentRules.IdCard);
        Assert.Equal("International passport", id.Label);
        Assert.Equal("A12345678", id.IdNumber);
        Assert.Contains(documents!, d => d.Category == ClientDocumentRules.UtilityBill && d.IdNumber == "0123456789");
    }

    [Fact]
    public async Task Approval_needs_two_guarantors_and_a_reference_and_an_approved_client_keeps_them()
    {
        var (clientId, marketer, controller) = await SetUpAsync();

        var client = await controller.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal(["2 more guarantors", "1 reference", "a face capture"], client!.ApprovalBlockers!);
        Assert.False(client.Actions!.CanApprove);
        var refused = await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("guarantor", await refused.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/guarantors",
            new SaveClientContactRequest("Chidi Obi", "0803", null, "12 Marina", "Brother", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/guarantors",
            new SaveClientContactRequest("Chidi Obi", "08031234567", null, null, "Brother", null))).StatusCode);

        var guarantors = new List<ClientContactResponse>();
        foreach (var name in new[] { "Chidi Obi", "Bola Ade" })
        {
            var created = await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/guarantors", new SaveClientContactRequest(name, "08031234567", null, "12 Marina, Lagos", "Colleague", "Trader"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            guarantors.Add((await created.Content.ReadFromJsonAsync<ClientContactResponse>(TestJson.Options))!);
        }

        Assert.Equal("+2348031234567", guarantors[0].Phone);
        Assert.Equal(HttpStatusCode.Created, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/references",
            new SaveClientContactRequest("Emeka Eze", "08039876543", null, null, "Pastor", null))).StatusCode);

        // Guarantors and a reference aren't enough while face capture is mandatory.
        Assert.Equal(["a face capture"], (await controller.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.ApprovalBlockers!);
        var noFace = await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { });
        Assert.Equal(HttpStatusCode.Conflict, noFace.StatusCode);
        Assert.Contains("face capture", await noFace.Content.ReadAsStringAsync());

        await EnrollFaceAsync(clientId);
        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        // Approved: locked until a controller grants edit privilege.
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.DeleteAsync($"/api/v1/clients/{clientId}/guarantors/{guarantors[0].Id}")).StatusCode);
        await GrantEditPrivilegeAsync(marketer, controller, clientId, "Guarantor moved abroad.");

        // Even then it can't drop below two guarantors — add a replacement first.
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.DeleteAsync($"/api/v1/clients/{clientId}/guarantors/{guarantors[0].Id}")).StatusCode);
        await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/guarantors", new SaveClientContactRequest("Ngozi Nwosu", "08031112222", null, "5 Broad St", "Aunt", null));
        Assert.Equal(HttpStatusCode.NoContent, (await marketer.DeleteAsync($"/api/v1/clients/{clientId}/guarantors/{guarantors[0].Id}")).StatusCode);

        // Only the onboarding marketer changes them.
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/references",
            new SaveClientContactRequest("Tunde Bello", "08031234567", null, null, "Friend", null))).StatusCode);
    }

    [Fact]
    public async Task Only_an_approved_client_can_apply_for_a_loan()
    {
        var (clientId, marketer, controller) = await SetUpAsync();
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await db.Clients.FindAsync(clientId))!.OfficeId!.Value;
            await TestDataSeeder.SeedApprovalRequirementsAsync(db, clientId);
        }

        object Application() => new { ClientType = "Client", ClientId = clientId, OfficeId = officeId, LoanProductId = 999_999, Amount = 50_000 };

        var pending = await marketer.PostAsJsonAsync("/api/v1/loan-applications", Application());
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Contains("pending approval", await pending.Content.ReadAsStringAsync());

        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        // Past the approval and face checks; this made-up product then stops the application.
        var approved = await marketer.PostAsJsonAsync("/api/v1/loan-applications", Application());
        Assert.DoesNotContain("pending approval", await approved.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Capture it under Biometrics", await approved.Content.ReadAsStringAsync());
        Assert.Null((await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.LoanBlocker);
    }

    private async Task EnrollFaceAsync(int clientId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        (await db.Clients.FindAsync(clientId))!.BiometricEnrolledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task An_approved_client_is_edited_only_with_granted_edit_privilege_and_goes_back_to_pending_until_approved_again()
    {
        var (clientId, marketer, controller) = await SetUpAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await TestDataSeeder.SeedApprovalRequirementsAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), clientId);
        }

        // Before approval there's nothing to request — the profile is still editable.
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest("Typo."))).StatusCode);
        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);

        var officeId = (await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.OfficeId;
        object Details(string address) => new { OfficeId = officeId, FirstName = "Ada", LastName = "Obi", DisplayName = "Ada Obi", Address = address };
        var locked = await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", Details("1 New Street"));
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Contains("Request edit privilege", await locked.Content.ReadAsStringAsync());

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.False(client!.Actions!.CanEditDetails);
        Assert.True(client.Actions.CanRequestEdit);

        // Only the onboarding marketer asks; a reason is required; only a controller decides.
        Assert.Equal(HttpStatusCode.BadRequest, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest(" "))).StatusCode);
        var requested = await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest("Client moved house."));
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var editRequest = (await requested.Content.ReadFromJsonAsync<ClientEditRequestResponse>(TestJson.Options))!;
        Assert.Equal(HttpStatusCode.Conflict, (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest("Again."))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PostAsJsonAsync($"/api/v1/client-edit-requests/{editRequest.Id}/approve", new { })).StatusCode);

        // Still active while the request waits.
        Assert.Equal(ClientStatus.Active, (await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.Status);
        var queue = await controller.GetFromJsonAsync<PagedResult<ClientEditRequestResponse>>("/api/v1/client-edit-requests", TestJson.Options);
        Assert.Contains(queue!.Items, r => r.Id == editRequest.Id && r.Reason == "Client moved house.");

        Assert.True((await controller.PostAsJsonAsync($"/api/v1/client-edit-requests/{editRequest.Id}/approve", new { })).IsSuccessStatusCode);

        // The first saved edit sends the client back to Pending; editing stays open until approved again.
        var edited = await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", Details("1 New Street"));
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal(ClientStatus.Pending, client!.Status);
        Assert.True(client.Actions!.EditPrivilegeOpen);
        Assert.True((await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/references",
            new SaveClientContactRequest("Tunde Bello", "08031234567", null, null, "Neighbour", null))).IsSuccessStatusCode);

        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);
        client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal(ClientStatus.Active, client!.Status);
        Assert.False(client.Actions!.EditPrivilegeOpen);
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", Details("2 Other Street"))).StatusCode);

        var history = await marketer.GetFromJsonAsync<List<ClientEditRequestResponse>>($"/api/v1/clients/{clientId}/edit-requests", TestJson.Options);
        Assert.Equal(ClientEditRequest.CompletedStatus, Assert.Single(history!).Status);
    }

    [Fact]
    public async Task A_refused_edit_request_needs_a_note_and_leaves_the_client_locked()
    {
        var (clientId, marketer, controller) = await SetUpAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await TestDataSeeder.SeedApprovalRequirementsAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), clientId);
        }

        await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { });
        var editRequest = await (await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest("Change phone.")))
            .Content.ReadFromJsonAsync<ClientEditRequestResponse>(TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, (await controller.PostAsJsonAsync($"/api/v1/client-edit-requests/{editRequest!.Id}/reject", new { })).StatusCode);
        Assert.True((await controller.PostAsJsonAsync($"/api/v1/client-edit-requests/{editRequest.Id}/reject", new ReviewDeletionRequest("Bring the new SIM registration first."))).IsSuccessStatusCode);

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal(ClientStatus.Active, client!.Status);
        Assert.False(client.Actions!.CanEditDetails);
        Assert.True(client.Actions.CanRequestEdit);
    }

    [Fact]
    public async Task No_two_clients_can_share_a_bvn()
    {
        var (clientId, marketer, _) = await SetUpAsync();
        var takenBvn = $"3{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await db.Clients.FindAsync(clientId))!.OfficeId!.Value;
            db.Clients.Add(new Client { FirstName = "Other", LastName = "Person", Bvn = takenBvn, OfficeId = officeId, Status = ClientStatus.Active, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var taken = await marketer.PutAsJsonAsync($"/api/v1/clients/{clientId}", new { OfficeId = officeId, FirstName = "Ada", LastName = "Obi", Bvn = takenBvn });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("BVN", await taken.Content.ReadAsStringAsync());

        // And onboarding refuses it too.
        var check = await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(takenBvn, "Ada Obi", null));
        Assert.Equal(HttpStatusCode.Conflict, check.StatusCode);
    }

    [Fact]
    public async Task A_client_with_an_active_loan_cannot_apply_for_another()
    {
        var (clientId, marketer, controller) = await SetUpAsync();
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await db.Clients.FindAsync(clientId))!.OfficeId!.Value;
            await TestDataSeeder.SeedApprovalRequirementsAsync(db, clientId);
        }

        Assert.True((await controller.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { })).IsSuccessStatusCode);
        await EnrollFaceAsync(clientId);
        Assert.Null((await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options))!.ActiveLoan);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.Add(new Domain.Loans.Loan
            {
                ClientId = clientId, OfficeId = officeId, ClientType = Domain.Loans.LoanClientType.Client,
                Status = Domain.Loans.LoanStatus.Disbursed, AccountNumber = "LN-TEST-1", ApprovedAmount = 50_000,
            });
            await db.SaveChangesAsync();
        }

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.Equal("Loan LN-TEST-1 is still running.", client!.ActiveLoan);

        var refused = await marketer.PostAsJsonAsync("/api/v1/loan-applications", new { ClientType = "Client", ClientId = clientId, OfficeId = officeId, LoanProductId = 1, Amount = 10_000 });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("active loan", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_group_page_lists_its_members_loans_and_applications()
    {
        var (clientId, marketer, _) = await SetUpAsync();
        int groupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = (await db.Clients.FindAsync(clientId))!;
            var group = new Domain.Groups.Group { Name = "Unity Traders", OfficeId = client.OfficeId, Status = Domain.Groups.GroupStatus.Pending };
            db.Groups.Add(group);
            await db.SaveChangesAsync();
            groupId = group.Id;
            db.GroupClients.Add(new Domain.Groups.GroupClient { GroupId = groupId, ClientId = clientId, Role = Domain.Groups.GroupMemberRoles.Leader, CreatedAt = DateTime.UtcNow });
            var product = new Domain.Loans.LoanProduct { Name = $"Group records {Guid.NewGuid():N}", MinimumPrincipal = 1000, MaximumPrincipal = 100_000, MinimumLoanTerm = 1, MaximumLoanTerm = 24 };
            db.LoanProducts.Add(product);
            await db.SaveChangesAsync();
            db.LoanApplications.Add(new Domain.Loans.LoanApplication
            {
                ClientType = Domain.Loans.LoanClientType.Client, ClientId = clientId, OfficeId = client.OfficeId, Amount = 40_000, LoanProductId = product.Id,
                Status = Domain.Loans.ApprovalStatus.Pending, CreatedAt = DateTime.UtcNow,
            });
            db.Loans.Add(new Domain.Loans.Loan
            {
                ClientId = clientId, OfficeId = client.OfficeId, ClientType = Domain.Loans.LoanClientType.Client, Status = Domain.Loans.LoanStatus.Closed,
                AccountNumber = "LN-OLD-1", ApprovedAmount = 150_000, ApprovedDate = new DateOnly(2026, 1, 10),
            });
            await db.SaveChangesAsync();
        }

        var summary = await marketer.GetFromJsonAsync<GroupSummaryResponse>($"/api/v1/groups/{groupId}/summary", TestJson.Options);
        Assert.Contains(summary!.LoanRecords, r => r.Kind == "application" && r.ClientId == clientId && r.Amount == 40_000 && r.Status == "Pending");
        Assert.Contains(summary.LoanRecords, r => r.Kind == "loan" && r.Reference == "LN-OLD-1" && r.ClientName == "Ada Obi");
        Assert.Equal(150_000, summary.CumulativeLoanAmount);
    }

    private static async Task GrantEditPrivilegeAsync(HttpClient marketer, HttpClient controller, int clientId, string reason)
    {
        var requested = await marketer.PostAsJsonAsync($"/api/v1/clients/{clientId}/edit-requests", new ReasonRequest(reason));
        Assert.True(requested.IsSuccessStatusCode, await requested.Content.ReadAsStringAsync());
        var editRequest = await requested.Content.ReadFromJsonAsync<ClientEditRequestResponse>(TestJson.Options);
        Assert.True((await controller.PostAsJsonAsync($"/api/v1/client-edit-requests/{editRequest!.Id}/approve", new { })).IsSuccessStatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient staff, int clientId, string? category, string? idType, string? idNumber, string fileName, int size)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(new byte[size]), "file", fileName } };
        if (category is not null) form.Add(new StringContent(category), "category");
        if (idType is not null) form.Add(new StringContent(idType), "idType");
        if (idNumber is not null) form.Add(new StringContent(idNumber), "idNumber");
        return await staff.PostAsync($"/api/v1/clients/{clientId}/documents", form);
    }

    private async Task<(int ClientId, HttpClient Marketer, HttpClient Controller)> SetUpAsync()
    {
        var marketerEmail = $"doc-marketer-{Guid.NewGuid():N}@bckash.test";
        var controllerEmail = $"doc-controller-{Guid.NewGuid():N}@bckash.test";
        int clientId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            var marketer = await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, controllerEmail, Password, UserTypeSlugs.Controller, officeId);
            var client = new Client { FirstName = "Ada", LastName = "Obi", DisplayName = "Ada Obi", OfficeId = officeId, Status = ClientStatus.Pending, CreatedById = marketer.Id, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }

        return (clientId, await SignInAsync(marketerEmail), await SignInAsync(controllerEmail));
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
