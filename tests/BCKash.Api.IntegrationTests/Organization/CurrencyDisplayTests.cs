using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Organization;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

public class CurrencyDisplayTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public CurrencyDisplayTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Any_staff_member_can_read_the_display_settings_but_not_every_setting()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "currency-staff@bckash.test", "clients.manage");
        await SetAsync(CurrencyDisplayKeys.Symbol, "$");
        await SetAsync(CurrencyDisplayKeys.Position, "right");

        var display = await staff.GetFromJsonAsync<DisplaySettingsResponse>("/api/v1/settings/display", TestJson.Options);
        var everything = await staff.GetAsync("/api/v1/settings");

        Assert.Equal("$", display!.CurrencySymbol);
        Assert.Equal("right", display.CurrencyPosition);
        Assert.Equal(HttpStatusCode.Forbidden, everything.StatusCode); // holds secrets
    }

    [Theory]
    [InlineData(CurrencyDisplayKeys.Symbol, " ")]
    [InlineData(CurrencyDisplayKeys.Position, "centre")]
    public async Task Invalid_display_values_are_rejected(string key, string value)
    {
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, $"currency-invalid-{key}@bckash.test", "settings.manage");

        var response = await admin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(key, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
