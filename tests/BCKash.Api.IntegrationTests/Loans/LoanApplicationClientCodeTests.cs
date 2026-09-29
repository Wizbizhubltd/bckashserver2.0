using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using BCKash.Application.Loans;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>Client confirmation codes for loans raised on a client's behalf (Settings → Notifications → Loan raised).</summary>
public class LoanApplicationClientCodeTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanApplicationClientCodeTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task With_codes_on_the_client_must_confirm_the_exact_application()
    {
        await SetCodesAsync(true);
        var staff = await StaffAsync("code-flow@bckash.test");
        var (clientId, productId) = await SeedClientAndProductAsync("Flow", "+2348031110001");

        // No code — refused.
        var noCode = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 5000));
        Assert.Equal(HttpStatusCode.BadRequest, noCode.StatusCode);
        Assert.Equal("client_code_required", await ReasonAsync(noCode));

        // The client is texted a code that tells them the amount.
        var sent = await (await staff.PostAsJsonAsync("/api/v1/loan-applications/client-codes", new RequestClientCodeRequest(clientId, productId, 5000)))
            .Content.ReadFromJsonAsync<ClientCodeResponse>(TestJson.Options);
        Assert.True(sent!.Required);
        var sms = SmsTo("+2348031110001");
        Assert.Contains("₦5,000", sms.Message);
        var code = Regex.Match(sms.Message, @"code is (\d{6})").Groups[1].Value;

        // Wrong code — counts an attempt.
        var wrong = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 5000, sent.CodeId, code == "000000" ? "111111" : "000000"));
        Assert.Equal("client_code_incorrect", await ReasonAsync(wrong));

        // Right code, different amount than the client was told — refused.
        var changedAmount = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 9000, sent.CodeId, code));
        Assert.Equal("client_code_mismatch", await ReasonAsync(changedAmount));

        // Right code, same application — created, and the code is linked to it.
        var created = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 5000, sent.CodeId, code));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var application = await created.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var used = await db.LoanApplicationClientCodes.SingleAsync(c => c.Id == sent.CodeId);
            Assert.NotNull(used.ConsumedAtUtc);
            Assert.Equal(application!.Id, used.LoanApplicationId);
        }

        // The same code can't raise a second application.
        var reused = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 5000, sent.CodeId, code));
        Assert.Equal("client_code_expired", await ReasonAsync(reused));

        // The confirmed amount can't be edited upwards afterwards; other details still can.
        var raised = await staff.PutAsJsonAsync($"/api/v1/loan-applications/{application!.Id}",
            new UpdateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 9000, 12, FrequencyType.Months, null));
        Assert.Equal(HttpStatusCode.Conflict, raised.StatusCode);
        var notesOnly = await staff.PutAsJsonAsync($"/api/v1/loan-applications/{application.Id}",
            new UpdateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 5000, 18, FrequencyType.Months, "Term agreed at 18 months"));
        Assert.Equal(HttpStatusCode.OK, notesOnly.StatusCode);
    }

    [Fact]
    public async Task A_second_code_cannot_be_requested_straight_away()
    {
        await SetCodesAsync(true);
        var staff = await StaffAsync("code-throttle@bckash.test");
        var (clientId, productId) = await SeedClientAndProductAsync("Throttle", "+2348031110002");

        var first = await staff.PostAsJsonAsync("/api/v1/loan-applications/client-codes", new RequestClientCodeRequest(clientId, productId, 2000));
        var second = await staff.PostAsJsonAsync("/api/v1/loan-applications/client-codes", new RequestClientCodeRequest(clientId, productId, 2000));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task A_client_with_no_mobile_or_email_cannot_be_sent_a_code()
    {
        await SetCodesAsync(true);
        var staff = await StaffAsync("code-nocontact@bckash.test");
        var (clientId, productId) = await SeedClientAndProductAsync("NoContact", null);

        var response = await staff.PostAsJsonAsync("/api/v1/loan-applications/client-codes", new RequestClientCodeRequest(clientId, productId, 2000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task With_codes_off_nothing_changes()
    {
        await SetCodesAsync(false);
        var staff = await StaffAsync("code-off@bckash.test");
        var (clientId, productId) = await SeedClientAndProductAsync("Off", "+2348031110004");

        var code = await (await staff.PostAsJsonAsync("/api/v1/loan-applications/client-codes", new RequestClientCodeRequest(clientId, productId, 2000)))
            .Content.ReadFromJsonAsync<ClientCodeResponse>(TestJson.Options);
        var created = await staff.PostAsJsonAsync("/api/v1/loan-applications", Application(clientId, productId, 2000));

        Assert.False(code!.Required);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static CreateLoanApplicationRequest Application(int clientId, int productId, decimal amount, int? codeId = null, string? code = null) =>
        new(LoanClientType.Client, null, null, null, clientId, null, productId, amount, 12, FrequencyType.Months, null, codeId, code);

    private Task<HttpClient> StaffAsync(string email) =>
        AuthenticatedClientFactory.CreateAsync(_factory, email, ["loan-applications.manage"]);

    private SentOtpSms SmsTo(string phone) =>
        _factory.Services.GetRequiredService<RecordingOtpSmsSender>().Sent.Last(s => s.ToPhone == phone);

    private static async Task<string?> ReasonAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
    }

    private async Task SetCodesAsync(bool on)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.SettingKey == ILoanApplicationClientCodeService.SettingKey);
        if (setting is null)
        {
            setting = new Setting { SettingKey = ILoanApplicationClientCodeService.SettingKey };
            db.Settings.Add(setting);
        }

        setting.SettingValue = on ? "1" : "0";
        await db.SaveChangesAsync();
    }

    private async Task<(int ClientId, int ProductId)> SeedClientAndProductAsync(string label, string? mobile)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var client = new Client { FirstName = label, LastName = "Client", Mobile = mobile, Status = ClientStatus.Active, AccountNo = $"CODE-{label}" };
        var product = new LoanProduct { Name = $"{label} Product", MinimumPrincipal = 1000, MaximumPrincipal = 10000, MinimumLoanTerm = 6, MaximumLoanTerm = 24 };
        db.Clients.Add(client);
        db.LoanProducts.Add(product);
        await db.SaveChangesAsync();
        return (client.Id, product.Id);
    }
}
