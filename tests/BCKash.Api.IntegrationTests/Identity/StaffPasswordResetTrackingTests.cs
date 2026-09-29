using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>
/// The staff list tracks a forced password reset: pending while the staff member is on the temporary
/// password, done once they set their own. Its own fixture, like StaffDefaultResetPasswordTests.
/// </summary>
public class StaffPasswordResetTrackingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StaffPasswordResetTrackingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("StaffPortal:DefaultResetPassword", "Bck@2027"));
    }

    [Fact]
    public async Task The_staff_list_shows_a_reset_as_pending_until_the_staff_member_sets_their_own_password()
    {
        var password = await ResetAndReadPasswordAsync("tracked");
        UserResponse Row(IEnumerable<UserResponse> rows) => rows.Single(u => u.Email == "bulk-default-tracked@bckash.test");

        using var admin = _factory.CreateClient();
        var adminTokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, admin, "bulk-default-admin-tracked@bckash.test", "Correct-Password1!");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        var pending = Row((await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/v1/users?pageSize=100", TestJson.Options))!.Items);
        Assert.True(pending.MustChangePassword);
        Assert.NotNull(pending.PasswordResetRequestedAt);
        Assert.Null(pending.PasswordChangedAt);

        using var staff = _factory.CreateClient();
        var staffTokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, staff, "bulk-default-tracked@bckash.test", password);
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffTokens.AccessToken);
        (await staff.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(password, "Their-Own-Password1!"))).EnsureSuccessStatusCode();

        var done = Row((await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/v1/users?pageSize=100", TestJson.Options))!.Items);
        Assert.False(done.MustChangePassword);
        Assert.True(done.PasswordChangedAt >= done.PasswordResetRequestedAt);
    }

    private async Task<string> ResetAndReadPasswordAsync(string suffix)
    {
        int staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            await TestDataSeeder.SeedUserAsync(db, $"bulk-default-admin-{suffix}@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            staffId = (await TestDataSeeder.SeedTypedUserAsync(db, $"bulk-default-{suffix}@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;
        }

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, $"bulk-default-admin-{suffix}@bckash.test", "Correct-Password1!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await client.PostAsJsonAsync("/api/v1/users/bulk/reset-password", new { userIds = new[] { staffId } }, TestJson.Options)).EnsureSuccessStatusCode();

        var emails = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var body = emails.Sent.Last(e => e.ToAddress == $"bulk-default-{suffix}@bckash.test").Body;
        return body.Split('\n').Single(l => l.StartsWith("Temporary password: ")).Replace("Temporary password: ", string.Empty).Trim();
    }
}
