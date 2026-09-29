using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Communications;

/// <summary>
/// Settings → Notifications → "SMS sending", the master switch for every SMS. Its own fixture:
/// switching SMS off changes delivery for every test sharing the database.
/// </summary>
public class SmsSwitchTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public SmsSwitchTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private RecordingOtpSmsSender CodeSms => _factory.Services.GetRequiredService<RecordingOtpSmsSender>();

    private RecordingSmsSender CampaignSms => _factory.Services.GetRequiredService<RecordingSmsSender>();

    private RecordingEmailSender Emails => (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    [Fact]
    public async Task Switching_sms_off_stops_every_sms_but_not_the_email_copy()
    {
        const string email = "sms-switch-staff@bckash.test";
        const string phone = "+2348031119999";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var user = await TestDataSeeder.SeedUserAsync(db, email, Password);
            user.Phone = phone;
            await db.SaveChangesAsync();
        }

        // On by default: signing in texts the code as well as emailing it.
        await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, _factory.CreateClient(), email, Password);
        Assert.Contains(CodeSms.Sent, s => s.ToPhone == phone);

        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sms-switch-sa@bckash.test");
        var off = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(ISmsSwitch.SettingKey, "0"));
        Assert.Equal(HttpStatusCode.Created, off.StatusCode);

        var smsBefore = CodeSms.Sent.Count(s => s.ToPhone == phone);
        var emailsBefore = Emails.Sent.Count(e => e.ToAddress == email);
        await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, _factory.CreateClient(), email, Password);
        Assert.Equal(smsBefore, CodeSms.Sent.Count(s => s.ToPhone == phone));
        Assert.True(Emails.Sent.Count(e => e.ToAddress == email) > emailsBefore);

        // Campaign messages go through the same switch.
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
            await sender.SendAsync(new SmsGateway { Id = 1, Url = "https://sms.example.test/send" }, phone, "Campaign message");
        }

        Assert.DoesNotContain(CampaignSms.Sent, s => s.ToPhone == phone);

        // An SMS campaign run while SMS is off is refused, not recorded as sent.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.SmsGateways.Add(new SmsGateway { Name = "BCKash", Url = "https://sms.example.test/send" });
            await db.SaveChangesAsync();
        }

        var campaigns = await AuthenticatedClientFactory.CreateAsync(_factory, "sms-switch-campaigns@bckash.test", ["campaigns.manage", "campaigns.run"]);
        var created = await campaigns.PostAsJsonAsync("/api/v1/campaigns", new SaveCampaignRequest(
            Type: CampaignType.Sms, Name: "Switched off", Description: null, ReportStartDate: null, ReportStartTime: null,
            RecurrenceType: null, RecurFrequency: null, RecurInterval: null, EmailRecipients: null, EmailSubject: null, Message: "Hello",
            EmailAttachmentFileFormat: null, RecipientsCategory: CampaignRecipientsCategory.AllClients, ReportAttachment: null, FromDay: null, ToDay: null,
            OfficeId: null, LoanOfficerId: null, LoanStatus: null, LoanProductId: null, Active: true));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var campaign = await created.Content.ReadFromJsonAsync<CampaignResponse>(TestJson.Options);

        var run = await campaigns.PostAsync($"/api/v1/campaigns/{campaign!.Id}/run", content: null);
        Assert.Equal(HttpStatusCode.Conflict, run.StatusCode);
        Assert.Contains("SMS sending is switched off", await run.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_switch_only_takes_on_or_off()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sms-switch-invalid@bckash.test");

        var response = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(ISmsSwitch.SettingKey, "maybe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
