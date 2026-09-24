using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BCKash.Infrastructure.Organization;

/// <summary>
/// Seeds reference data at startup — idempotent (NFR-10): each table is only seeded if
/// currently empty, so this is safe to run on every boot. No legacy data rows exist to
/// migrate (see Phase 1 plan notes), so this seeds what's honestly seedable: the full
/// ISO-3166 country list (genuine, universal reference data), Nigeria's states and LGAs
/// (see <see cref="NigeriaLocationSeedData"/>), and a small set of
/// clearly-labeled sensible defaults for Currencies/PaymentTypes — NOT presented as
/// migrated legacy data, just enough for the system to be usable out of the box.
/// </summary>
public class ReferenceDataSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ReferenceDataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();

        if (!await db.Countries.AnyAsync(cancellationToken))
        {
            db.Countries.AddRange(IsoCountrySeedData.Countries.Select(c => new Country { Sortname = c.Sortname, Name = c.Name }));
        }

        if (!await db.Currencies.AnyAsync(cancellationToken))
        {
            db.Currencies.Add(new Currency { Name = "Nigerian Naira", Code = "NGN", Symbol = "₦", Decimals = "2", Active = true });
        }

        if (!await db.PaymentTypes.AnyAsync(cancellationToken))
        {
            db.PaymentTypes.AddRange(
                new PaymentType { Name = "Cash", IsCash = true },
                new PaymentType { Name = "Cheque", IsCash = false },
                new PaymentType { Name = "Bank Transfer", IsCash = false });
        }

        if (!await db.States.AnyAsync(cancellationToken))
        {
            db.States.AddRange(NigeriaLocationSeedData.States.Select(s => new State
            {
                Name = s.Name,
                Lgas = s.Lgas.Select(lga => new Lga { Name = lga }).ToList(),
            }));
        }

        await db.SaveChangesAsync(cancellationToken);

        // Offices created before office codes existed get one on the next boot.
        var officesWithoutCode = await db.Offices.IgnoreQueryFilters().Where(o => o.OfficeCode == null).ToListAsync(cancellationToken);
        foreach (var office in officesWithoutCode)
        {
            office.OfficeCode = await OfficeCodeGenerator.GenerateUniqueAsync(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
