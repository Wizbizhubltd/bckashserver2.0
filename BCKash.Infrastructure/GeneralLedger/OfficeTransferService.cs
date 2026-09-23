using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

/// <summary>
/// FR-GL-5. Both journal-entry legs post against the same caller-chosen `glAccountId` (a
/// clearing/cash-in-transit account) — opposite sides, same amount, so the pair is balanced by
/// construction regardless of which account is chosen. See IOfficeTransferService's doc comment
/// for why the account is caller-supplied rather than inferred from the offices.
/// </summary>
public class OfficeTransferService : IOfficeTransferService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public OfficeTransferService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task<OfficeTransferResult> CreateAsync(
        int fromOfficeId, int toOfficeId, int? currencyId, decimal amount, int glAccountId, DateOnly date, string? notes,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new OfficeTransferResult(OfficeTransferOutcome.InvalidAmount);
        }

        if (fromOfficeId == toOfficeId)
        {
            return new OfficeTransferResult(OfficeTransferOutcome.SameOffice);
        }

        if (!await _db.GlAccounts.AnyAsync(a => a.Id == glAccountId, cancellationToken))
        {
            return new OfficeTransferResult(OfficeTransferOutcome.AccountNotFound);
        }

        if (!await _closureGuard.IsDatePostableAsync(fromOfficeId, date, cancellationToken)
            || !await _closureGuard.IsDatePostableAsync(toOfficeId, date, cancellationToken))
        {
            return new OfficeTransferResult(OfficeTransferOutcome.ClosurePeriod);
        }

        var transaction = new OfficeTransaction
        {
            FromOfficeId = fromOfficeId,
            ToOfficeId = toOfficeId,
            CurrencyId = currencyId,
            Amount = amount,
            Date = date,
            Notes = notes,
        };
        _db.OfficeTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        var reference = $"TRANSFER-{transaction.Id}";
        _db.GlJournalEntries.Add(new GlJournalEntry
        {
            OfficeId = fromOfficeId,
            GlAccountId = glAccountId,
            CurrencyId = currencyId,
            TransactionType = GlTransactionType.TransferFund,
            Credit = amount,
            Reference = reference,
            Date = date,
            Narration = notes,
            ManualEntry = false,
            Approved = true,
        });
        _db.GlJournalEntries.Add(new GlJournalEntry
        {
            OfficeId = toOfficeId,
            GlAccountId = glAccountId,
            CurrencyId = currencyId,
            TransactionType = GlTransactionType.TransferFund,
            Debit = amount,
            Reference = reference,
            Date = date,
            Narration = notes,
            ManualEntry = false,
            Approved = true,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new OfficeTransferResult(OfficeTransferOutcome.Success, transaction);
    }
}
