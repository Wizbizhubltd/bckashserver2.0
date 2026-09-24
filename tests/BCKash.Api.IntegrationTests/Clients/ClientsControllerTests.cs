using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateClientRequest NewClientRequest(string firstName, string lastName, string? mobile = null, string? bvn = null, int? officeId = null) =>
        new(
            Bvn: bvn, CountryId: null, OfficeId: officeId, StaffId: null, ReferredById: null, ExternalId: null,
            Title: null, FirstName: firstName, MiddleName: null, LastName: lastName, FullName: $"{firstName} {lastName}",
            IncorporationNumber: null, DisplayName: $"{firstName} {lastName}", Picture: null, Mobile: mobile, Phone: null,
            Email: null, Gender: null, ClientType: ClientType.Individual, MaritalStatus: null, Dob: null,
            Street: null, Ward: null, District: null, Region: null, Address: null, JoinedDate: null,
            Occupation: null, PostalCode: null, Country: null, State: null, City: null);

    [Fact]
    public async Task Create_and_fetch_a_client()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "client-crud@bckash.test", "clients.manage");

        var response = await client.PostAsJsonAsync("/api/v1/clients", NewClientRequest("Ada", "Okafor"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        Assert.Equal("Ada", created!.FirstName);
        Assert.Matches(new Regex("^CL[0-9]{8}$"), created.AccountNo);
        Assert.Null(created.OldAccountNo);
        Assert.Equal(ClientStatus.Pending, created.Status);

        var getResponse = await client.GetAsync($"/api/v1/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Two_clients_created_back_to_back_get_distinct_sequential_account_numbers()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "client-sequence@bckash.test", "clients.manage");

        var first = await (await client.PostAsJsonAsync("/api/v1/clients", NewClientRequest("First", "Client"))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        var second = await (await client.PostAsJsonAsync("/api/v1/clients", NewClientRequest("Second", "Client"))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        Assert.NotEqual(first!.AccountNo, second!.AccountNo);
    }

    [Fact]
    public async Task Update_applies_editable_fields_and_leaves_account_number_and_status_untouched()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "client-update@bckash.test", "clients.manage");

        var created = await (await client.PostAsJsonAsync("/api/v1/clients", NewClientRequest("Bola", "Adeyemi"))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        var updateRequest = new UpdateClientRequest(
            Bvn: "12345678901", CountryId: null, OfficeId: null, StaffId: null, ReferredById: null, ExternalId: null,
            Title: "Mr", FirstName: "Bola", MiddleName: null, LastName: "Adeyemi-Updated", FullName: "Bola Adeyemi-Updated",
            IncorporationNumber: null, DisplayName: "Bola Adeyemi-Updated", Picture: null, Mobile: "08011112222", Phone: null,
            Email: "bola@example.test", Gender: null, ClientType: ClientType.Individual, MaritalStatus: MaritalStatus.Single, Dob: null,
            Street: null, Ward: null, District: null, Region: null, Address: null, JoinedDate: null,
            Occupation: "Trader", PostalCode: null, Country: null, State: null, City: null);

        var response = await client.PutAsJsonAsync($"/api/v1/clients/{created!.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        Assert.Equal("Adeyemi-Updated", updated!.LastName);
        Assert.Equal("12345678901", updated.Bvn);
        Assert.Equal(created.AccountNo, updated.AccountNo);
        Assert.Equal(ClientStatus.Pending, updated.Status);
    }

    public static IEnumerable<object[]> StatusTransitionCases()
    {
        ClientStatus[] statuses = [ClientStatus.Pending, ClientStatus.Active, ClientStatus.Inactive, ClientStatus.Declined, ClientStatus.Closed];
        string[] transitions = ["activate", "deactivate", "reactivate", "decline", "close"];

        foreach (var status in statuses)
        {
            foreach (var transition in transitions)
            {
                yield return [status, transition];
            }
        }
    }

    [Theory]
    [MemberData(nameof(StatusTransitionCases))]
    public async Task Status_transition_only_succeeds_from_its_valid_predecessor(ClientStatus fromStatus, string transition)
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, $"client-transition-{fromStatus}-{transition}@bckash.test", "clients.manage");

        int clientId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var entity = new Client { Status = fromStatus, AccountNo = $"SEED-{Guid.NewGuid():N}" };
            db.Clients.Add(entity);
            await db.SaveChangesAsync();
            clientId = entity.Id;
        }

        var expectedAllowed = transition switch
        {
            "activate" => ClientStatusTransitionRules.CanActivate(fromStatus),
            "deactivate" => ClientStatusTransitionRules.CanDeactivate(fromStatus),
            "reactivate" => ClientStatusTransitionRules.CanReactivate(fromStatus),
            "decline" => ClientStatusTransitionRules.CanDecline(fromStatus),
            "close" => ClientStatusTransitionRules.CanClose(fromStatus),
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };

        var response = transition switch
        {
            "activate" => await httpClient.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new ActivateClientRequest(null)),
            "deactivate" => await httpClient.PostAsJsonAsync($"/api/v1/clients/{clientId}/deactivate", new ReasonRequest("Integration test reason")),
            "reactivate" => await httpClient.PostAsync($"/api/v1/clients/{clientId}/reactivate", null),
            "decline" => await httpClient.PostAsJsonAsync($"/api/v1/clients/{clientId}/decline", new ReasonRequest("Integration test reason")),
            "close" => await httpClient.PostAsJsonAsync($"/api/v1/clients/{clientId}/close", new ReasonRequest("Integration test reason")),
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };

        if (!expectedAllowed)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var updated = await assertDb.Clients.FirstAsync(c => c.Id == clientId);

        switch (transition)
        {
            case "activate":
                Assert.Equal(ClientStatus.Active, updated.Status);
                Assert.NotNull(updated.ActivatedDate);
                Assert.NotNull(updated.ActivatedById);
                break;
            case "deactivate":
                Assert.Equal(ClientStatus.Inactive, updated.Status);
                Assert.NotNull(updated.InactiveDate);
                Assert.NotNull(updated.InactiveById);
                Assert.Equal("Integration test reason", updated.InactiveReason);
                break;
            case "reactivate":
                Assert.Equal(ClientStatus.Active, updated.Status);
                Assert.NotNull(updated.ReactivatedDate);
                Assert.NotNull(updated.ReactivatedById);
                break;
            case "decline":
                Assert.Equal(ClientStatus.Declined, updated.Status);
                Assert.NotNull(updated.DeclinedDate);
                Assert.NotNull(updated.DeclinedById);
                Assert.Equal("Integration test reason", updated.DeclinedReason);
                break;
            case "close":
                Assert.Equal(ClientStatus.Closed, updated.Status);
                Assert.NotNull(updated.ClosedDate);
                Assert.NotNull(updated.ClosedById);
                Assert.Equal("Integration test reason", updated.ClosedReason);
                break;
        }

        var audited = await assertDb.AuditTrail
            .Where(a => a.Module == "Client" && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(audited);
    }

    [Fact]
    public async Task Deactivate_without_a_reason_is_rejected()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "client-reason-required@bckash.test", "clients.manage");

        var created = await (await httpClient.PostAsJsonAsync("/api/v1/clients", NewClientRequest("Reason", "Required"))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        await httpClient.PostAsJsonAsync($"/api/v1/clients/{created!.Id}/activate", new ActivateClientRequest(null));

        var response = await httpClient.PostAsJsonAsync($"/api/v1/clients/{created.Id}/deactivate", new ReasonRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_filter_and_pagination_narrow_the_client_list()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "client-search@bckash.test", "clients.manage");

        var unique = Guid.NewGuid().ToString("N")[..8];
        await httpClient.PostAsJsonAsync("/api/v1/clients", NewClientRequest($"Zainab{unique}", "Search", mobile: $"0800{unique}", bvn: $"BVN{unique}"));
        await httpClient.PostAsJsonAsync("/api/v1/clients", NewClientRequest("Someone", "Else"));

        var byName = await httpClient.GetFromJsonAsync<PagedResult<ClientListItemResponse>>($"/api/v1/clients?search=Zainab{unique}", TestJson.Options);
        Assert.Equal(1, byName!.TotalCount);
        Assert.Equal($"Zainab{unique}", byName.Items[0].FirstName);

        var byBvn = await httpClient.GetFromJsonAsync<PagedResult<ClientListItemResponse>>($"/api/v1/clients?bvn=BVN{unique}", TestJson.Options);
        Assert.Equal(1, byBvn!.TotalCount);

        var byMobile = await httpClient.GetFromJsonAsync<PagedResult<ClientListItemResponse>>($"/api/v1/clients?mobile=0800{unique}", TestJson.Options);
        Assert.Equal(1, byMobile!.TotalCount);

        var noMatch = await httpClient.GetFromJsonAsync<PagedResult<ClientListItemResponse>>($"/api/v1/clients?search=NoSuchClient{unique}", TestJson.Options);
        Assert.Equal(0, noMatch!.TotalCount);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_the_configured_maximum()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "client-pagesize@bckash.test", "clients.manage");

        var result = await httpClient.GetFromJsonAsync<PagedResult<ClientListItemResponse>>("/api/v1/clients?pageSize=5000", TestJson.Options);

        Assert.Equal(100, result!.PageSize);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reads_are_allowed_without_the_manage_permission_but_writes_are_not()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "client-readonly@bckash.test", "Correct-Password1!");

        var loginClient = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, loginClient, user.Email, "Correct-Password1!");

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var readResponse = await httpClient.GetAsync("/api/v1/clients");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        var writeResponse = await httpClient.PostAsJsonAsync("/api/v1/clients", NewClientRequest("No", "Permission"));
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }
}
