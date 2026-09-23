using BCKash.Application.GeneralLedger;
using BCKash.Application.PayrollProcessing;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;

namespace BCKash.Infrastructure.PayrollProcessing;

public class PayrollGlPostingService : IPayrollGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public PayrollGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostPayrollRunAsync(BCKash.Domain.Payroll.Payroll payroll, CancellationToken cancellationToken = default)
    {
        var lines = PayrollGlPostingRules.ForPayrollRun(payroll, payroll.PaidAmount);
        if (lines.Count == 0)
        {
            return;
        }

        var date = payroll.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(payroll.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"PAYROLL-{payroll.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = payroll.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = GlTransactionType.Payroll,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                PayrollTransactionId = payroll.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
