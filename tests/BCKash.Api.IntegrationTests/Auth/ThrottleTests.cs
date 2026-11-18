using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

// The factory configures LoginThrottle:MaxFailedAttempts=3, IpMaxFailedAttempts=6, WindowMinutes=30, LockoutMinutes=15.
public class ThrottleTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ThrottleTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;

        // The lockout is off unless a super admin switches it on (see LoginLockoutToggleTests).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        if (!db.Settings.Any(st => st.SettingKey == LoginLockoutRules.SettingKey))
        {
            db.Settings.Add(new Setting { SettingKey = LoginLockoutRules.SettingKey, SettingValue = "1" });
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Account_locks_out_after_the_configured_number_of_failed_attempts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "throttle-lockout@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Even the CORRECT password is now rejected — the account is locked, not just the bad guess.
        var lockedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task Lockout_clears_once_the_cooldown_window_has_passed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "throttle-cooldown@bckash.test", "Correct-Password1!");

        // Simulate 3 failed attempts far enough in the past (older than the 15-minute lockout,
        // but still inside the 15-minute counting window) instead of sleeping in the test.
        var pastAttempt = DateTime.UtcNow.AddMinutes(-20);
        db.Throttles.AddRange(
            new Throttle { Type = "login", UserId = user.Id, CreatedAt = pastAttempt, UpdatedAt = pastAttempt },
            new Throttle { Type = "login", UserId = user.Id, CreatedAt = pastAttempt, UpdatedAt = pastAttempt },
            new Throttle { Type = "login", UserId = user.Id, CreatedAt = pastAttempt, UpdatedAt = pastAttempt });
        await db.SaveChangesAsync();

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Locked_out_message_says_how_long_to_wait()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "throttle-message@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
        }

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Contains("try again in 15 minutes", await locked.Content.ReadAsStringAsync());
        Assert.NotNull(locked.Headers.RetryAfter);
    }

    [Fact]
    public async Task A_correct_password_clears_earlier_failed_attempts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "throttle-clear@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
        }
        var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // Two more typos after a good sign-in: 2, not 4, so still under the limit of 3.
        for (var i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
        }
        var again = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Colleagues_sharing_an_IP_do_not_lock_each_other_out()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var throttle = scope.ServiceProvider.GetRequiredService<ILoginThrottleService>();
        var careless = await TestDataSeeder.SeedUserAsync(db, "throttle-careless@bckash.test", "Correct-Password1!");
        var colleague = await TestDataSeeder.SeedUserAsync(db, "throttle-colleague@bckash.test", "Correct-Password1!");
        const string officeIp = "203.0.113.10";

        // One colleague locks their own account from the shared office IP...
        for (var i = 0; i < 3; i++)
        {
            await throttle.RecordFailedAttemptAsync(careless.Id, officeIp);
        }

        Assert.NotNull(await throttle.GetLockedUntilAsync(careless.Id, officeIp));
        // ...but the next person at the same IP can still sign in.
        Assert.Null(await throttle.GetLockedUntilAsync(colleague.Id, officeIp));

        // Only a flood of failures from the IP (password spraying) locks the IP itself.
        for (var i = 0; i < 3; i++)
        {
            await throttle.RecordFailedAttemptAsync(null, officeIp);
        }
        Assert.NotNull(await throttle.GetLockedUntilAsync(colleague.Id, officeIp));
        Assert.Null(await throttle.GetLockedUntilAsync(colleague.Id, "198.51.100.7"));
    }

    [Fact]
    public async Task A_locked_account_can_reset_its_password_and_sign_in_straight_away()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "throttle-reset@bckash.test", "Old-Password1!");

        using var client = _factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
        }
        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Old-Password1!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(user.Email));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var challenge = (await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponse>())!.ChallengeToken;

        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var code = Regex.Match(emailSender.Sent.Last(e => e.ToAddress == user.Email).Body, @"reset code is (\d{6})").Groups[1].Value;

        var reset = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "New-Password1!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "New-Password1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
