using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Communications;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Communications;

/// <summary>
/// Phase 9's first acceptance criterion: each recipient category (arrears, overdue, birthdays,
/// etc.) correctly selects the right client set against a test dataset. Seeds a controlled
/// dataset directly via the DbContext (offices/clients/loans/repayment schedules), then asserts
/// each category returns exactly the expected client IDs via the real
/// POST /api/campaigns/preview-recipients endpoint.
/// </summary>
public class CampaignRecipientTargetingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public CampaignRecipientTargetingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Each_recipient_category_selects_the_correct_client_set()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int activeClientId, pendingClientId, disbursedLoanClientId, arrearsClientId, npaClientId, birthdayClientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();

            var office = new Office { Name = "Targeting Test Office", Active = true };
            db.Offices.Add(office);
            await db.SaveChangesAsync();

            var activeClient = new Client { OfficeId = office.Id, FirstName = "Active", LastName = "Client", Status = ClientStatus.Active, Mobile = "0800000001", Email = "active@test.local" };
            var pendingClient = new Client { OfficeId = office.Id, FirstName = "Pending", LastName = "Client", Status = ClientStatus.Pending, Mobile = "0800000002", Email = "pending@test.local" };
            var disbursedLoanClient = new Client { OfficeId = office.Id, FirstName = "Disbursed", LastName = "Borrower", Status = ClientStatus.Active, Mobile = "0800000003", Email = "disbursed@test.local" };
            var arrearsClient = new Client { OfficeId = office.Id, FirstName = "Arrears", LastName = "Borrower", Status = ClientStatus.Active, Mobile = "0800000004", Email = "arrears@test.local" };
            var npaClient = new Client { OfficeId = office.Id, FirstName = "Npa", LastName = "Borrower", Status = ClientStatus.Active, Mobile = "0800000005", Email = "npa@test.local" };
            var birthdayClient = new Client { OfficeId = office.Id, FirstName = "Birthday", LastName = "Client", Status = ClientStatus.Active, Mobile = "0800000006", Email = "birthday@test.local", Dob = new DateOnly(1990, today.Month, today.Day) };
            db.Clients.AddRange(activeClient, pendingClient, disbursedLoanClient, arrearsClient, npaClient, birthdayClient);
            await db.SaveChangesAsync();

            activeClientId = activeClient.Id;
            pendingClientId = pendingClient.Id;
            disbursedLoanClientId = disbursedLoanClient.Id;
            arrearsClientId = arrearsClient.Id;
            npaClientId = npaClient.Id;
            birthdayClientId = birthdayClient.Id;

            // A plain disbursed loan, fully up to date — belongs in ActiveLoans only.
            var disbursedLoan = new Loan { OfficeId = office.Id, ClientId = disbursedLoanClient.Id, Status = LoanStatus.Disbursed, AccountNumber = "LN-ACTIVE" };
            // A disbursed loan with one unpaid, past-due installment — belongs in ActiveLoans + LoansInArrears, but NOT OverdueLoans (IsNpa false).
            var arrearsLoan = new Loan { OfficeId = office.Id, ClientId = arrearsClient.Id, Status = LoanStatus.Disbursed, AccountNumber = "LN-ARREARS", IsNpa = false };
            // A disbursed loan already flagged NPA — belongs in ActiveLoans + OverdueLoans (LoansInArrears is decided purely by schedule lateness, and this one also has a past-due row, so it lands there too).
            var npaLoan = new Loan { OfficeId = office.Id, ClientId = npaClient.Id, Status = LoanStatus.Disbursed, AccountNumber = "LN-NPA", IsNpa = true };
            db.Loans.AddRange(disbursedLoan, arrearsLoan, npaLoan);
            await db.SaveChangesAsync();

            db.LoanRepaymentSchedules.AddRange(
                new BCKash.Domain.Loans.LoanRepaymentSchedule { LoanId = disbursedLoan.Id, DueDate = today.AddDays(30), Paid = false, Principal = 1000m },
                new BCKash.Domain.Loans.LoanRepaymentSchedule { LoanId = arrearsLoan.Id, DueDate = today.AddDays(-10), Paid = false, Principal = 1000m },
                new BCKash.Domain.Loans.LoanRepaymentSchedule { LoanId = npaLoan.Id, DueDate = today.AddDays(-100), Paid = false, Principal = 1000m });
            await db.SaveChangesAsync();
        }

        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "campaign-targeting@bckash.test", "campaigns.manage");

        var allIds = await PreviewAsync(client, CampaignRecipientsCategory.AllClients);
        Assert.Contains(activeClientId, allIds);
        Assert.Contains(pendingClientId, allIds);

        var activeIds = await PreviewAsync(client, CampaignRecipientsCategory.ActiveClients);
        Assert.Contains(activeClientId, activeIds);
        Assert.DoesNotContain(pendingClientId, activeIds);

        var prospectiveIds = await PreviewAsync(client, CampaignRecipientsCategory.ProspectiveClients);
        Assert.Contains(pendingClientId, prospectiveIds);
        Assert.DoesNotContain(activeClientId, prospectiveIds);

        var activeLoanIds = await PreviewAsync(client, CampaignRecipientsCategory.ActiveLoans);
        Assert.Contains(disbursedLoanClientId, activeLoanIds);
        Assert.Contains(arrearsClientId, activeLoanIds);
        Assert.Contains(npaClientId, activeLoanIds);
        Assert.DoesNotContain(activeClientId, activeLoanIds); // has no loan at all

        var arrearsIds = await PreviewAsync(client, CampaignRecipientsCategory.LoansInArrears);
        Assert.Contains(arrearsClientId, arrearsIds);
        Assert.Contains(npaClientId, arrearsIds); // also past-due
        Assert.DoesNotContain(disbursedLoanClientId, arrearsIds); // not past due

        var overdueIds = await PreviewAsync(client, CampaignRecipientsCategory.OverdueLoans);
        Assert.Contains(npaClientId, overdueIds);
        Assert.DoesNotContain(arrearsClientId, overdueIds); // in arrears but not (yet) NPA
        Assert.DoesNotContain(disbursedLoanClientId, overdueIds);

        var birthdayIds = await PreviewAsync(client, CampaignRecipientsCategory.HappyBirthday);
        Assert.Contains(birthdayClientId, birthdayIds);
        Assert.DoesNotContain(activeClientId, birthdayIds);
    }

    private static async Task<List<int>> PreviewAsync(HttpClient client, CampaignRecipientsCategory category)
    {
        var request = new SaveCampaignRequest(
            Type: CampaignType.Sms, Name: $"Preview-{category}", Description: null, ReportStartDate: null, ReportStartTime: null,
            RecurrenceType: null, RecurFrequency: null, RecurInterval: null, EmailRecipients: null, EmailSubject: null, Message: "Hi",
            EmailAttachmentFileFormat: null, RecipientsCategory: category, ReportAttachment: null, FromDay: null, ToDay: null,
            OfficeId: null, LoanOfficerId: null, LoanStatus: null, LoanProductId: null, Active: true);

        var response = await client.PostAsJsonAsync("/api/v1/campaigns/preview-recipients", request);
        var recipients = await response.Content.ReadFromJsonAsync<List<CampaignRecipientResponse>>(TestJson.Options);
        return recipients!.Select(r => r.ClientId).ToList();
    }
}
