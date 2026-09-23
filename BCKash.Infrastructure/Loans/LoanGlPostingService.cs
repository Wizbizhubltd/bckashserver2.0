using BCKash.Application.GeneralLedger;
using BCKash.Application.Loans;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Real GL posting for loan transactions (replaces Phase 5's NoOpLoanGlPostingService) — see
/// ILoanGlPostingService's doc comment and docs/gl-posting-spec.md for the full caveat and
/// worked examples. Every method is a no-op (posting skipped, not silently faked) when the
/// product isn't fully configured for GL or the transaction date falls on/before an active
/// closure for its office — both are deliberate "don't post something wrong" choices, not bugs.
/// </summary>
public class LoanGlPostingService : ILoanGlPostingService
{
    private readonly BCKashDbContext _db;
    private readonly IGlClosureGuard _closureGuard;

    public LoanGlPostingService(BCKashDbContext db, IGlClosureGuard closureGuard)
    {
        _db = db;
        _closureGuard = closureGuard;
    }

    public async Task PostDisbursementAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = LoanGlPostingRules.ForDisbursement(product, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.Disbursement, loan, transaction, cancellationToken);
    }

    public async Task PostRepaymentAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = LoanGlPostingRules.ForRepayment(
            product,
            transaction.Principal ?? 0m,
            transaction.Interest ?? 0m,
            transaction.Fee ?? 0m,
            transaction.Penalty ?? 0m,
            transaction.OverpaymentDerived ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.Repayment, loan, transaction, cancellationToken);
    }

    public async Task PostWriteOffAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = LoanGlPostingRules.ForWriteOff(
            product, transaction.Principal ?? 0m, transaction.Interest ?? 0m, transaction.Fee ?? 0m, transaction.Penalty ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.WriteOff, loan, transaction, cancellationToken);
    }

    public async Task PostWriteOffRecoveryAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = LoanGlPostingRules.ForWriteOffRecovery(product, transaction.Amount ?? 0m);
        await WriteBatchAsync(lines, GlTransactionType.RepaymentRecovery, loan, transaction, cancellationToken);
    }

    public async Task PostWaiverAsync(Loan loan, LoanTransaction transaction, LoanRepaymentComponent component, decimal amount, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var lines = LoanGlPostingRules.ForWaiver(product, component, amount);
        await WriteBatchAsync(lines, GlTransactionType.Repayment, loan, transaction, cancellationToken);
    }

    private async Task WriteBatchAsync(
        IReadOnlyList<GlPostingLine> lines, GlTransactionType transactionType, Loan loan, LoanTransaction transaction, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var date = transaction.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await _closureGuard.IsDatePostableAsync(loan.OfficeId, date, cancellationToken))
        {
            return;
        }

        var reference = $"LOAN-{transaction.TransactionType}-{transaction.Id}";
        foreach (var line in lines)
        {
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = loan.OfficeId,
                GlAccountId = line.GlAccountId,
                TransactionType = transactionType,
                TransactionSubType = line.SubType,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                LoanId = loan.Id,
                LoanTransactionId = transaction.Id,
                Date = date,
                ManualEntry = false,
                Approved = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
