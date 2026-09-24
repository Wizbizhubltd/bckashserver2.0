using System.Security.Cryptography;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Organization;

internal static class OfficeCodeGenerator
{
    /// <summary>
    /// A code not already used by any office. The unique index on offices.office_code is the real
    /// guarantee; with 10^8 possible codes a retry here is already vanishingly rare.
    /// </summary>
    public static async Task<string> GenerateUniqueAsync(BCKashDbContext db, CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = OfficeCodeFormat.Generate(() => RandomNumberGenerator.GetInt32(10));
            if (!await db.Offices.IgnoreQueryFilters().AnyAsync(o => o.OfficeCode == code, cancellationToken))
            {
                return code;
            }
        }
    }
}
