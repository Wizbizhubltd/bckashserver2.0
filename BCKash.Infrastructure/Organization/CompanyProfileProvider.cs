using BCKash.Application.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Organization;

public class CompanyProfileProvider : ICompanyProfileProvider
{
    private readonly BCKashDbContext _db;

    public CompanyProfileProvider(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<CompanyProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Settings
            .Where(s => CompanyProfileKeys.All.Contains(s.SettingKey))
            .Select(s => new { s.Id, s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);

        // The legacy table doesn't stop a key appearing twice — the newest row wins.
        var values = rows
            .GroupBy(r => r.SettingKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().SettingValue?.Trim());

        // Anything that wouldn't pass validation today (legacy data predates the rules) is treated as unset.
        string? Valid(string key) =>
            values.GetValueOrDefault(key) is { Length: > 0 } v && CompanyProfileRules.Validate(key, v) is null ? v : null;

        return new CompanyProfile(
            Valid(CompanyProfileKeys.Name) ?? CompanyProfile.DefaultName,
            Valid(CompanyProfileKeys.Email),
            Valid(CompanyProfileKeys.Website),
            Valid(CompanyProfileKeys.PortalAddress),
            Valid(CompanyProfileKeys.Address));
    }
}
