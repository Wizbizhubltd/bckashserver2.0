using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>Reads <see cref="FaceCaptureRules.SettingKey"/> — whether face capture is mandatory right now.</summary>
public static class FaceCapturePolicy
{
    public static async Task<bool> IsRequiredAsync(BCKashDbContext db, CancellationToken cancellationToken = default)
    {
        var value = await db.Settings
            .Where(s => s.SettingKey == FaceCaptureRules.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return FaceCaptureRules.IsRequired(value);
    }
}
