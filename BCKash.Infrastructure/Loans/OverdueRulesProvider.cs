using BCKash.Application.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class OverdueRulesProvider : IOverdueRulesProvider
{
    private readonly BCKashDbContext _db;

    public OverdueRulesProvider(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<OverdueRules> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Settings
            .Where(s => OverdueRuleKeys.All.Contains(s.SettingKey))
            .Select(s => new { s.Id, s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        var values = rows.GroupBy(r => r.SettingKey).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().SettingValue?.Trim());

        // Invalid legacy values fall back to the safe default (0 days / off) rather than failing.
        int Days(string key) =>
            int.TryParse(values.GetValueOrDefault(key), out var d) && d is >= 0 and <= OverdueRuleKeys.MaxDays ? d : 0;

        return new OverdueRules(
            Days(OverdueRuleKeys.RepaymentOverdueDays),
            Days(OverdueRuleKeys.LoanOverdueDays),
            values.GetValueOrDefault(OverdueRuleKeys.AutoApplyPenalty) is "1" or "true");
    }
}
