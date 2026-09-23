using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

/// <summary>
/// The seeder already ran once when the factory's host started (it's a registered
/// IHostedService) — these tests check what it left behind, plus that running it again
/// doesn't duplicate anything (NFR-10 idempotency).
/// </summary>
public class ReferenceDataSeederTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ReferenceDataSeederTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Countries_currencies_and_payment_types_are_seeded_on_startup()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();

        Assert.True(await db.Countries.CountAsync() > 100);
        Assert.Contains(await db.Countries.ToListAsync(), c => c.Sortname == "NG" && c.Name == "Nigeria");
        Assert.True(await db.Currencies.AnyAsync(c => c.Code == "NGN"));
        Assert.True(await db.PaymentTypes.AnyAsync(p => p.Name == "Cash" && p.IsCash));
    }

    [Fact]
    public async Task Re_running_the_seeder_does_not_duplicate_rows()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var countryCountBefore = await db.Countries.CountAsync();
        var currencyCountBefore = await db.Currencies.CountAsync();
        var paymentTypeCountBefore = await db.PaymentTypes.CountAsync();

        var seeder = scope.ServiceProvider.GetServices<IHostedService>().OfType<ReferenceDataSeeder>().Single();
        await seeder.StartAsync(CancellationToken.None);

        Assert.Equal(countryCountBefore, await db.Countries.CountAsync());
        Assert.Equal(currencyCountBefore, await db.Currencies.CountAsync());
        Assert.Equal(paymentTypeCountBefore, await db.PaymentTypes.CountAsync());
    }
}
