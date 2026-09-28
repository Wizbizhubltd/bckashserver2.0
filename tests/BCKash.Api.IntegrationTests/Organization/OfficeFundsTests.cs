using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Organization;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

/// <summary>Office business operations: bank accounts, funding acknowledged or disputed by the manager, and loans drawing on office funds.</summary>
public class OfficeFundsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public OfficeFundsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_first_bank_account_is_the_default_and_the_default_cannot_be_deactivated()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "fund-accounts@bckash.test");
        var officeId = await SeedOfficeAsync("Accounts Office", managerId: null);

        var bad = await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/bank-accounts", new SaveOfficeBankAccountRequest("Guaranty Trust Bank (GTBank)", "Accounts Office", "12345", false));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // not a 10-digit NUBAN

        var first = await Read<OfficeBankAccountResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/bank-accounts", new SaveOfficeBankAccountRequest("guaranty trust bank (gtbank)", "Accounts Office", "0123456789", false)));
        Assert.True(first.IsDefault);
        Assert.Equal("Guaranty Trust Bank (GTBank)", first.BankName); // stored in the list's spelling

        var unknownBank = await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/bank-accounts", new SaveOfficeBankAccountRequest("Bank of Nowhere", "Accounts Office", "1112223334", false));
        Assert.Equal(HttpStatusCode.BadRequest, unknownBank.StatusCode);
        Assert.Contains(await admin.GetFromJsonAsync<List<BankResponse>>("/api/v1/banks", TestJson.Options) ?? [], b => b.Name == "Zenith Bank");

        var second = await Read<OfficeBankAccountResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/bank-accounts", new SaveOfficeBankAccountRequest("Access Bank", "Accounts Office", "9876543210", true)));
        Assert.True(second.IsDefault);

        var deactivateDefault = await admin.PostAsync($"/api/v1/offices/{officeId}/bank-accounts/{second.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.BadRequest, deactivateDefault.StatusCode);
        var deactivateOther = await admin.PostAsync($"/api/v1/offices/{officeId}/bank-accounts/{first.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateOther.StatusCode);
    }

    [Fact]
    public async Task Funding_needs_a_manager_and_a_default_account_and_counts_once_the_manager_acknowledges()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "fund-ack-admin@bckash.test");
        var manager = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-ack-manager@bckash.test", "clients.manage");
        var otherStaff = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-ack-other@bckash.test", "clients.manage");
        var managerId = await UserIdAsync("fund-ack-manager@bckash.test");

        var noManager = await SeedOfficeAsync("No Manager Office", managerId: null, withAccount: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/offices/{noManager}/fundings", new FundOfficeRequest(1000, "TRF-001", null, null))).StatusCode);

        var noAccount = await SeedOfficeAsync("No Account Office", managerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/offices/{noAccount}/fundings", new FundOfficeRequest(1000, "TRF-002", null, null))).StatusCode);

        var officeId = await SeedOfficeAsync("Funded Office", managerId, withAccount: true);
        var funding = await Read<OfficeFundingResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/fundings", new FundOfficeRequest(500_000, "TRF-100", null, "Q4 float")));
        Assert.Equal(OfficeFundingStatus.PendingAcknowledgement, funding.Status);

        // The same transfer can't be recorded twice.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/fundings", new FundOfficeRequest(500_000, "trf-100", null, null))).StatusCode);

        // Pending funding doesn't count yet.
        Assert.Equal(0m, (await OperationsAsync(admin, officeId)).Balance);

        // Only the office's manager can acknowledge — not the funder, not other staff.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/office-fundings/{funding.Id}/acknowledge", new FundingCommentRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStaff.PostAsJsonAsync($"/api/v1/office-fundings/{funding.Id}/acknowledge", new FundingCommentRequest(null))).StatusCode);

        var acknowledged = await Read<OfficeFundingResponse>(await manager.PostAsJsonAsync($"/api/v1/office-fundings/{funding.Id}/acknowledge", new FundingCommentRequest("Received")));
        Assert.Equal(OfficeFundingStatus.Acknowledged, acknowledged.Status);

        var operations = await OperationsAsync(manager, officeId);
        Assert.Equal(500_000m, operations.Balance);
        Assert.Single(operations.Entries);
        Assert.Contains(operations.Activity, a => a.Type == OfficeFundEventType.FundingAcknowledged && a.Comment == "Received");
        Assert.Contains(operations.Activity, a => a.Type == OfficeFundEventType.FundingSent);
    }

    [Fact]
    public async Task A_dispute_needs_a_reason_and_a_bank_statement_and_a_disputed_funding_can_be_cancelled()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "fund-dispute-admin@bckash.test");
        var manager = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-dispute-manager@bckash.test", "clients.manage");
        var officeId = await SeedOfficeAsync("Disputed Office", await UserIdAsync("fund-dispute-manager@bckash.test"), withAccount: true);
        var funding = await Read<OfficeFundingResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/fundings", new FundOfficeRequest(200_000, "TRF-200", null, null)));

        var noStatement = await manager.PostAsync($"/api/v1/office-fundings/{funding.Id}/dispute", new MultipartFormDataContent { { new StringContent("Only 150,000 arrived in the account"), "reason" } });
        Assert.Equal(HttpStatusCode.BadRequest, noStatement.StatusCode);

        var form = new MultipartFormDataContent
        {
            { new StringContent("Only 150,000 arrived in the account"), "reason" },
            { new ByteArrayContent("%PDF-1.4 statement"u8.ToArray()), "statement", "statement.pdf" },
        };
        var disputed = await Read<OfficeFundingResponse>(await manager.PostAsync($"/api/v1/office-fundings/{funding.Id}/dispute", form));
        Assert.Equal(OfficeFundingStatus.Disputed, disputed.Status);
        Assert.Equal("statement.pdf", disputed.DisputeDocumentName);

        var statement = await admin.GetAsync($"/api/v1/office-fundings/{funding.Id}/statement");
        Assert.Equal(HttpStatusCode.OK, statement.StatusCode);

        var cancelled = await Read<OfficeFundingResponse>(await admin.PostAsJsonAsync($"/api/v1/office-fundings/{funding.Id}/cancel", new FundingCommentRequest("Re-sending the correct amount")));
        Assert.Equal(OfficeFundingStatus.Cancelled, cancelled.Status);
        Assert.Equal(0m, (await OperationsAsync(admin, officeId)).Balance);
    }

    [Fact]
    public async Task When_loans_draw_on_office_funds_approval_and_disbursement_need_the_money()
    {
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "fund-loans-admin@bckash.test");
        var manager = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-loans-manager@bckash.test", "clients.manage");
        var lender = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-loans-lender@bckash.test", ["loan-applications.approve", "loan-servicing.manage"]);
        var officeId = await SeedOfficeAsync("Lending Office", await UserIdAsync("fund-loans-manager@bckash.test"), withAccount: true);
        await SetAsync(IOfficeFundService.RequireFundsSettingKey, "1");

        try
        {
            var first = await SeedApplicationAsync(officeId, 60_000);
            var refused = await lender.PostAsJsonAsync($"/api/v1/loan-applications/{first}/approve", new ApproveLoanApplicationRequest(60_000, null));
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

            var funding = await Read<OfficeFundingResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/fundings", new FundOfficeRequest(100_000, "TRF-300", null, null)));
            await manager.PostAsJsonAsync($"/api/v1/office-fundings/{funding.Id}/acknowledge", new FundingCommentRequest(null));

            var approved = await Read<LoanApplicationResponse>(await lender.PostAsJsonAsync($"/api/v1/loan-applications/{first}/approve", new ApproveLoanApplicationRequest(60_000, null)));

            // 60,000 is now committed, so a second 60,000 can't be approved against the 100,000.
            var second = await SeedApplicationAsync(officeId, 60_000);
            Assert.Equal(HttpStatusCode.Conflict, (await lender.PostAsJsonAsync($"/api/v1/loan-applications/{second}/approve", new ApproveLoanApplicationRequest(60_000, null))).StatusCode);

            var disbursed = await lender.PostAsJsonAsync($"/api/v1/loans/{approved.LoanId}/disburse", new DisburseLoanRequest(null, 60_000, null));
            Assert.Equal(HttpStatusCode.OK, disbursed.StatusCode);

            var operations = await OperationsAsync(admin, officeId);
            Assert.Equal(40_000m, operations.Balance);
            Assert.Contains(operations.Entries, e => e.Type == OfficeFundEntryType.LoanDisbursement && e.Amount == -60_000m && e.LoanId == approved.LoanId);
        }
        finally
        {
            await SetAsync(IOfficeFundService.RequireFundsSettingKey, "0");
        }
    }

    [Fact]
    public async Task A_manager_can_be_assigned_on_its_own_and_is_logged()
    {
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, "fund-assign-admin@bckash.test", "organization.manage");
        await AuthenticatedClientFactory.CreateAsync(_factory, "fund-assign-manager@bckash.test", "clients.manage");
        var managerId = await UserIdAsync("fund-assign-manager@bckash.test");
        var officeId = await SeedOfficeAsync("Assign Office", managerId: null);

        var assigned = await Read<OfficeResponse>(await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/manager", new AssignOfficeManagerRequest(managerId)));
        Assert.Equal(managerId, assigned.ManagerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/offices/{officeId}/manager", new AssignOfficeManagerRequest(999_999))).StatusCode);

        using var scope = _factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().OfficeFundEvents
            .AnyAsync(e => e.OfficeId == officeId && e.Type == OfficeFundEventType.ManagerAssigned));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static Task<OfficeBusinessOperationsResponse> OperationsAsync(HttpClient client, int officeId) =>
        client.GetFromJsonAsync<OfficeBusinessOperationsResponse>($"/api/v1/offices/{officeId}/business-operations", TestJson.Options)!;

    private async Task<int> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private async Task<int> SeedOfficeAsync(string name, int? managerId, bool withAccount = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var office = new Office { Name = name, ManagerId = managerId, Active = true };
        db.Offices.Add(office);
        await db.SaveChangesAsync();
        if (withAccount)
        {
            db.OfficeBankAccounts.Add(new OfficeBankAccount { OfficeId = office.Id, BankName = "GTBank", AccountName = name, AccountNumber = "0011223344", IsDefault = true });
            await db.SaveChangesAsync();
        }

        return office.Id;
    }

    private async Task<int> SeedApplicationAsync(int officeId, decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var client = new Client { FirstName = "Fund", LastName = "Borrower", Status = ClientStatus.Active, OfficeId = officeId, AccountNo = $"FUND-{Guid.NewGuid():N}"[..20] };
        var product = new LoanProduct { Name = $"Fund Product {Guid.NewGuid():N}", MinimumPrincipal = 1000, MaximumPrincipal = 1_000_000, MinimumLoanTerm = 1, MaximumLoanTerm = 24 };
        db.Clients.Add(client);
        db.LoanProducts.Add(product);
        await db.SaveChangesAsync();
        var application = new LoanApplication
        {
            ClientType = LoanClientType.Client, ClientId = client.Id, OfficeId = officeId, LoanProductId = product.Id,
            Amount = amount, LoanTerm = 6, LoanTermType = FrequencyType.Months, Status = ApprovalStatus.Pending,
        };
        db.LoanApplications.Add(application);
        await db.SaveChangesAsync();
        return application.Id;
    }

    private async Task SetAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting is null)
        {
            setting = new Setting { SettingKey = key };
            db.Settings.Add(setting);
        }

        setting.SettingValue = value;
        await db.SaveChangesAsync();
    }
}
