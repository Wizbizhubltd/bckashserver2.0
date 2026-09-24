using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Communications;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Communications;

/// <summary>
/// Phase 9's second acceptance criterion: a scheduled campaign fires at its configured time and
/// updates last/next run correctly. No scheduler exists in this codebase (same precedent as
/// Phase 8's payroll/expense recurrence) — "fires" means the admin-triggered run-due endpoint
/// picks it up once its NextRunDate is due.
/// </summary>
public class CampaignSchedulingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public CampaignSchedulingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Due_scheduled_campaign_fires_and_advances_next_run()
    {
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = new Office { Name = "Campaign Scheduling Office", Active = true };
            db.Offices.Add(office);
            await db.SaveChangesAsync();

            db.Clients.Add(new Client { OfficeId = office.Id, FirstName = "Recurring", LastName = "Recipient", Status = ClientStatus.Active, Email = "recurring@bckash.test" });
            await db.SaveChangesAsync();
            officeId = office.Id;
        }

        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "campaign-scheduling@bckash.test", ["campaigns.manage", "campaigns.run"]);

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1); // already due
        var created = await (await client.PostAsJsonAsync("/api/v1/campaigns", new SaveCampaignRequest(
            Type: CampaignType.Email, Name: "Monthly Newsletter", Description: null,
            ReportStartDate: dueDate, ReportStartTime: null,
            RecurrenceType: CampaignRecurrenceType.Schedule, RecurFrequency: CampaignRecurFrequency.Months, RecurInterval: "1",
            EmailRecipients: null, EmailSubject: "Newsletter", Message: "Hello!", EmailAttachmentFileFormat: null,
            RecipientsCategory: CampaignRecipientsCategory.ActiveClients, ReportAttachment: null, FromDay: null, ToDay: null,
            OfficeId: officeId.ToString(), LoanOfficerId: null, LoanStatus: null, LoanProductId: null, Active: true)))
            .Content.ReadFromJsonAsync<CampaignResponse>(TestJson.Options);

        Assert.Equal(0, created!.NumberOfRuns);
        Assert.Null(created.LastRunDate);

        var runResponse = await client.PostAsync("/api/v1/campaigns/run-due", content: null);
        Assert.True(runResponse.IsSuccessStatusCode);
        var runResult = await runResponse.Content.ReadFromJsonAsync<RunDueResponse>(TestJson.Options);
        Assert.Equal(1, runResult!.Ran);

        var afterRun = await client.GetFromJsonAsync<CampaignResponse>($"/api/v1/campaigns/{created.Id}", TestJson.Options);
        Assert.Equal(1, afterRun!.NumberOfRuns);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), afterRun.LastRunDate);
        Assert.Equal(1, afterRun.NumberOfRecipients); // the one active client seeded above
        Assert.Equal(dueDate.AddMonths(1), afterRun.NextRunDate);
        Assert.True(afterRun.Sent);

        // Running again immediately does nothing further — nothing else is due yet.
        var secondRun = await (await client.PostAsync("/api/v1/campaigns/run-due", content: null)).Content.ReadFromJsonAsync<RunDueResponse>(TestJson.Options);
        Assert.Equal(0, secondRun!.Ran);
    }

    private record RunDueResponse(int Ran);
}
