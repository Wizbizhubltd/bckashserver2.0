using BCKash.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCKash.Api.IntegrationTests.Data;

/// <summary>
/// Proves the EF Core model (all configured entities/relationships) is internally
/// consistent — part of Phase 0's "EF Core model matches the production schema"
/// acceptance criterion. Runs against SQLite, not MySQL, since no MySQL instance is
/// available in CI yet; re-validate against real MySQL/Pomelo once one is (see
/// DEVELOPMENT_PHASES.md Phase 0 notes).
/// </summary>
public class BCKashDbContextModelTests
{
    [Fact]
    public void Model_builds_and_schema_can_be_created()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BCKashDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new BCKashDbContext(options);

        context.Database.EnsureCreated();

        Assert.NotEmpty(context.Model.GetEntityTypes());
    }
}
