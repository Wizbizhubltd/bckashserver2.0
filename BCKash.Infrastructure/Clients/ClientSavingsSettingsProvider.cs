using BCKash.Application.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IClientSavingsSettingsProvider"/>.</summary>
public class ClientSavingsSettingsProvider : IClientSavingsSettingsProvider
{
    private readonly BCKashDbContext _db;

    public ClientSavingsSettingsProvider(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<ClientSavingsSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Settings
            .Where(s => ClientSavingsSettingKeys.All.Contains(s.SettingKey))
            .Select(s => new { s.Id, s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        var values = rows.GroupBy(r => r.SettingKey).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().SettingValue);

        // Invalid values fall back to the defaults rather than failing.
        decimal Fraction(string key, decimal fallback, decimal max) =>
            ClientSavingsSettingKeys.ParsePercent(values.GetValueOrDefault(key)) is { } percent && percent >= 0 && percent <= max ? percent / 100 : fallback;

        return new ClientSavingsSettings(
            Fraction(ClientSavingsSettingKeys.Rate, ClientSavingsSettings.Default.Rate, ClientSavingsSettingKeys.MaxRatePercent),
            Fraction(ClientSavingsSettingKeys.EarlyWithdrawalFee, ClientSavingsSettings.Default.EarlyWithdrawalFeeRate, 100));
    }
}
