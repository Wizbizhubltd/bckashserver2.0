using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BCKash.Api.IntegrationTests;

/// <summary>
/// Boots the real Api host against a per-test-class SQLite file instead of MySQL (no
/// MySQL instance is available in this environment — see Phase 0 plan) while keeping
/// every other piece of the pipeline (auth, authorization, the audit interceptor)
/// exactly as configured in Program.cs.
/// </summary>
public class BCKashWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"bckash-test-{Guid.NewGuid():N}.db");

    public const string TestSigningKey = "integration-test-signing-key-must-be-at-least-32-bytes-long";
    public const string TestIssuer = "BCKash.Api.Tests";
    public const string TestAudience = "BCKash.Api.Tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Testing:UseSqlite", "true");
        builder.UseSetting("ConnectionStrings:BCKashDb", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Jwt:Issuer", TestIssuer);
        builder.UseSetting("Jwt:Audience", TestAudience);
        builder.UseSetting("LoginThrottle:MaxFailedAttempts", "3");
        // WindowMinutes > LockoutMinutes on purpose: it lets ThrottleTests plant a failed
        // attempt old enough to be past its lockout cooldown but still inside the counting
        // window, isolating the LockoutMinutes-specific unlock path from window expiry.
        builder.UseSetting("LoginThrottle:WindowMinutes", "30");
        builder.UseSetting("LoginThrottle:LockoutMinutes", "15");

        builder.ConfigureServices(services =>
        {
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<BCKashDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
