using BCKash.Application.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Organization;

public class CurrencyDisplayProvider : ICurrencyDisplayProvider
{
    private readonly BCKashDbContext _db;

    public CurrencyDisplayProvider(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<CurrencyDisplay> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Settings
            .Where(s => CurrencyDisplayKeys.All.Contains(s.SettingKey))
            .Select(s => new { s.Id, s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        var values = rows.GroupBy(r => r.SettingKey).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().SettingValue?.Trim());

        var symbol = values.GetValueOrDefault(CurrencyDisplayKeys.Symbol);
        var position = values.GetValueOrDefault(CurrencyDisplayKeys.Position);
        return new CurrencyDisplay(
            CurrencyDisplayKeys.Validate(CurrencyDisplayKeys.Symbol, symbol) is null ? symbol! : CurrencyDisplay.Default.Symbol,
            position == "right" ? CurrencySymbolPosition.Right : CurrencySymbolPosition.Left);
    }
}
