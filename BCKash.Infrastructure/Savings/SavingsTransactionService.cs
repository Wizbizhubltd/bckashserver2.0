using BCKash.Application.GeneralLedger;
using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

public class SavingsTransactionService : ISavingsTransactionService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISavingsGlPostingService _glPostingService;
    private readonly IGlJournalEntryService _glJournalEntryService;

    public SavingsTransactionService(
        BCKashDbContext db, ICurrentUserContext currentUser, ISavingsGlPostingService glPostingService, IGlJournalEntryService glJournalEntryService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
        _glJournalEntryService = glJournalEntryService;
    }

    public async Task<SavingsTransactionWriteResult> RecordDepositAsync(int accountId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.InvalidAmount);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == accountId, cancellationToken);
        if (account is null)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanTransact(account.Status))
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.InvalidAccountStatus);
        }

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var newBalance = (account.Balance ?? 0m) + amount;

        var transaction = new SavingsTransaction
        {
            Savings = account,
            OfficeId = account.OfficeId,
            TransactionType = SavingsTransactionType.Deposit,
            Amount = amount,
            Credit = amount,
            Balance = newBalance,
            Date = effectiveDate,
            Status = SavingsTransactionStatus.Approved,
            Reversible = true,
            CreatedById = _currentUser.UserId,
            Notes = notes,
        };
        _db.SavingsTransactions.Add(transaction);

        account.Balance = newBalance;
        account.Deposits = (account.Deposits ?? 0m) + amount;
        account.ModifiedById = _currentUser.UserId;
        account.ModifiedDate = effectiveDate;

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostDepositAsync(account, transaction, cancellationToken);

        return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.Success, transaction);
    }

    public async Task<SavingsTransactionWriteResult> RecordWithdrawalAsync(int accountId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.InvalidAmount);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == accountId, cancellationToken);
        if (account is null)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanTransact(account.Status))
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.InvalidAccountStatus);
        }

        var floor = SavingsBalanceRules.EffectiveFloor(account.AllowOverdraft, account.MinimumBalance, account.OverdraftLimit);
        if (!SavingsBalanceRules.CanWithdraw(account.Balance ?? 0m, amount, floor))
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.InsufficientBalance);
        }

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var newBalance = (account.Balance ?? 0m) - amount;

        var transaction = new SavingsTransaction
        {
            Savings = account,
            OfficeId = account.OfficeId,
            TransactionType = SavingsTransactionType.Withdrawal,
            Amount = amount,
            Debit = amount,
            Balance = newBalance,
            Date = effectiveDate,
            Status = SavingsTransactionStatus.Approved,
            Reversible = true,
            CreatedById = _currentUser.UserId,
            Notes = notes,
        };
        _db.SavingsTransactions.Add(transaction);

        account.Balance = newBalance;
        account.Withdrawals = (account.Withdrawals ?? 0m) + amount;
        account.ModifiedById = _currentUser.UserId;
        account.ModifiedDate = effectiveDate;

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostWithdrawalAsync(account, transaction, cancellationToken);

        return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.Success, transaction);
    }

    /// <summary>
    /// Direct undo of this transaction's own balance effect — correct for the common case
    /// (reversing the most recent transaction) but not a full replay-based recomputation if
    /// reversals happen out of chronological order, the same documented limitation as
    /// LoanRepaymentService.ReverseAsync (Phase 5).
    /// </summary>
    public async Task<SavingsTransactionWriteResult> ReverseAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.SavingsTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (transaction is null)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.TransactionNotFound);
        }

        if (transaction.Reversed)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.AlreadyReversed);
        }

        if (!transaction.Reversible)
        {
            return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.NotReversible);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == transaction.SavingsId, cancellationToken);
        if (account is not null)
        {
            if (transaction.Credit is > 0)
            {
                account.Balance = (account.Balance ?? 0m) - transaction.Credit.Value;
                account.Deposits = (account.Deposits ?? 0m) - transaction.Credit.Value;
            }
            else if (transaction.Debit is > 0)
            {
                account.Balance = (account.Balance ?? 0m) + transaction.Debit.Value;
                account.Withdrawals = (account.Withdrawals ?? 0m) - transaction.Debit.Value;
            }

            account.ModifiedById = _currentUser.UserId;
        }

        transaction.Reversed = true;
        transaction.ReversalType = SavingsTransactionReversalType.User;
        transaction.ModifiedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _glJournalEntryService.ReverseByReferenceAsync($"SAVINGS-{transaction.TransactionType}-{transaction.Id}", cancellationToken);

        return new SavingsTransactionWriteResult(SavingsTransactionWriteOutcome.Success, transaction);
    }
}
