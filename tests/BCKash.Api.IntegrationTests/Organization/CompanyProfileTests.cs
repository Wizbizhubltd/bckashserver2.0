using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Application.Organization;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

/// <summary>The company profile on the Settings → Organisation tab is validated on save and names the organisation in outgoing messages.</summary>
public class CompanyProfileTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public CompanyProfileTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(CompanyProfileKeys.Name, "  ")]
    [InlineData(CompanyProfileKeys.Email, "not-an-email")]
    [InlineData(CompanyProfileKeys.Website, "www.acme.ng")]
    [InlineData(CompanyProfileKeys.PortalAddress, "http://www.")]
    public async Task Invalid_company_profile_values_are_rejected(string key, string value)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"profile-invalid-{key}@bckash.test", "settings.manage");

        var response = await client.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(key, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Company_name_is_saved_trimmed_and_cannot_be_removed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "profile-name@bckash.test", "settings.manage");
        var id = await UpsertAsync(CompanyProfileKeys.Name, "Old Name");

        var update = await client.PutAsJsonAsync($"/api/v1/settings/{id}", new UpdateSettingRequest("  Acme Microfinance  "));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var saved = await client.GetFromJsonAsync<SettingResponse>($"/api/v1/settings/{id}");
        Assert.Equal("Acme Microfinance", saved!.SettingValue);

        var delete = await client.DeleteAsync($"/api/v1/settings/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
    }

    [Fact]
    public async Task Outgoing_messages_use_the_company_profile()
    {
        await UpsertAsync(CompanyProfileKeys.Name, "Acme Microfinance");
        await UpsertAsync(CompanyProfileKeys.Email, "hello@acme.ng");
        await UpsertAsync(CompanyProfileKeys.PortalAddress, "https://portal.acme.ng");
        await UpsertAsync(CompanyProfileKeys.Website, "not a url"); // legacy junk — must not be sent

        // Signing in sends the login code.
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, "profile-messages@bckash.test", "users.manage");
        var emails = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var loginCode = emails.Sent.Last(e => e.ToAddress == "profile-messages@bckash.test");
        Assert.Equal("Your Acme Microfinance login code", loginCode.Subject);

        // A password reset emails the staff member their new credentials.
        int staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            staffId = (await TestDataSeeder.SeedUserAsync(db, "profile-staff@bckash.test", "Correct-Password1!", permissionSlug: "users.manage")).Id;
        }

        var reset = await admin.PostAsync($"/api/v1/users/{staffId}/reset-password", null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var credentials = emails.Sent.Last(e => e.ToAddress == "profile-staff@bckash.test");
        Assert.Equal("Your Acme Microfinance password has been reset", credentials.Subject);
        Assert.Contains("Sign in at https://portal.acme.ng", credentials.Body);
        Assert.EndsWith("Acme Microfinance\nhello@acme.ng", credentials.Body);
        Assert.DoesNotContain("not a url", credentials.Body);
    }

    /// <summary>Writes straight to the table, as legacy data would be — bypassing the endpoint's validation.</summary>
    private async Task<int> UpsertAsync(string key, string value)
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
        return setting.Id;
    }
}
