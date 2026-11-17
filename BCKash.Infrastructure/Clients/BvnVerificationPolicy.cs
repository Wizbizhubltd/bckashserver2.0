using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>Reads <see cref="BvnVerificationRules.SettingKey"/> — whether onboarding needs a verified BVN right now.</summary>
public static class BvnVerificationPolicy
{
    public static async Task<bool> IsRequiredAsync(BCKashDbContext db, CancellationToken cancellationToken = default)
    {
        var value = await db.Settings
            .Where(s => s.SettingKey == BvnVerificationRules.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return BvnVerificationRules.IsRequired(value);
    }
}
