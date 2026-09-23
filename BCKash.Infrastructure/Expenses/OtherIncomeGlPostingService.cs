using BCKash.Application.Expenses;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.Expenses;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Expenses;

public class OtherIncomeGlPostingService : IOtherIncomeGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public OtherIncomeGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostApprovalAsync(OtherIncome income, CancellationToken cancellationToken = default)
    {
        var incomeType = await _db.OtherIncomeTypes.FirstOrDefaultAsync(t => t.Id == income.OtherIncomeTypeId, cancellationToken);
        if (incomeType is null)
        {
            return;
        }

        var lines = OtherIncomeGlPostingRules.ForApproval(incomeType, income.Amount);
        if (lines.Count == 0)
        {
            return;
        }

        var date = income.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(income.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"OTHER-INCOME-{income.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = income.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = GlTransactionType.Income,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                TransactionId = income.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
