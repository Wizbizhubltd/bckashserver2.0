using BCKash.Application.GeneralLedger;
using BCKash.Application.Savings;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

/// <summary>
/// Real GL posting for savings transactions — mirrors LoanGlPostingService's exact shape
/// (Phase 6). A no-op (posting skipped, not silently faked) when the product isn't fully
/// configured for GL or the transaction date falls on/before an active closure for its office.
/// </summary>
public class SavingsGlPostingService : ISavingsGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public SavingsGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostDepositAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await ProductAsync(account, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = SavingsGlPostingRules.ForDeposit(product, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.Deposit, account, transaction, cancellationToken);
    }

    public async Task PostWithdrawalAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await ProductAsync(account, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = SavingsGlPostingRules.ForWithdrawal(product, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.Withdrawal, account, transaction, cancellationToken);
    }

    public async Task PostInterestAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await ProductAsync(account, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = SavingsGlPostingRules.ForInterest(product, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.Interest, account, transaction, cancellationToken);
    }

    public async Task PostChargeAsync(SavingsAccount account, SavingsTransaction transaction, bool isPenalty, CancellationToken cancellationToken = default)
    {
        var product = await ProductAsync(account, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = SavingsGlPostingRules.ForCharge(product, isPenalty, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.PayCharge, account, transaction, cancellationToken);
    }

    private Task<SavingsProduct?> ProductAsync(SavingsAccount account, CancellationToken cancellationToken) =>
        _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == account.SavingsProductId, cancellationToken);

    private async Task WriteBatchAsync(
        IReadOnlyList<GlPostingLine> lines, GlTransactionType transactionType, SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var date = transaction.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(account.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"SAVINGS-{transaction.TransactionType}-{transaction.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = account.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = transactionType,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                SavingsId = account.Id,
                SavingsTransactionId = transaction.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
