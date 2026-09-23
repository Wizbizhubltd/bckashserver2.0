using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

// The factory configures LoginThrottle:MaxFailedAttempts=3, WindowMinutes=30, LockoutMinutes=15.
public class ThrottleTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ThrottleTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
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
            var failed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Even the CORRECT password is now rejected — the account is locked, not just the bad guess.
        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
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
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
