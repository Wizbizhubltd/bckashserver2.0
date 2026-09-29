using Amazon.S3;
using BCKash.Application.Clients;
using BCKash.Application.Files;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // The API loads the developer's .env, which may hold real AWS keys — tests must never reach AWS.
        builder.UseSetting("Aws:AccessKeyId", string.Empty);
        builder.UseSetting("Aws:SecretAccessKey", string.Empty);
        builder.UseSetting("Aws:S3Bucket", string.Empty);
        builder.UseSetting("StaffPortal:Url", "https://office.bckash.test");

        // Fixed pass marks, whatever a developer has tuned in their .env.
        builder.UseSetting("Biometrics:FaceMatchThreshold", "90");
        builder.UseSetting("Biometrics:LivenessThreshold", "80");
        builder.UseSetting("LoginThrottle:MaxFailedAttempts", "3");
        // WindowMinutes > LockoutMinutes on purpose: it lets ThrottleTests plant a failed
        // attempt old enough to be past its lockout cooldown but still inside the counting
        // window, isolating the LockoutMinutes-specific unlock path from window expiry.
        builder.UseSetting("LoginThrottle:WindowMinutes", "30");
        builder.UseSetting("LoginThrottle:LockoutMinutes", "15");

        builder.ConfigureServices(services =>
        {
            // Never the real BVN gateway in tests — the predictable fake instead.
            services.RemoveAll<IBvnVerificationProvider>();
            services.AddSingleton<IBvnVerificationProvider, FakeBvnVerificationProvider>();

            // Nor AWS: face captures come from the fake, files stay on local disk — whatever .env says.
            services.RemoveAll<IAmazonS3>();
            services.RemoveAll<IFileStorageService>();
            services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<LocalDiskFileStorageService>());
            services.RemoveAll<IFaceBiometrics>();
            services.AddSingleton<FakeFaceBiometrics>();
            services.AddSingleton<IFaceBiometrics>(sp => sp.GetRequiredService<FakeFaceBiometrics>());

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
