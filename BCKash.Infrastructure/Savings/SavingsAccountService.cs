using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

/// <summary>
/// FR-SAV-2. Account numbers follow the same retry-on-collision generation loop as Phase 2's
/// ClientService.CreateAsync, using SavingsAccountNumberFormat ("SV" + 8-digit sequence).
/// </summary>
public class SavingsAccountService : ISavingsAccountService
{
    private const int MaxAccountNumberRetries = 5;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISavingsGlPostingService _glPostingService;

    public SavingsAccountService(BCKashDbContext db, ICurrentUserContext currentUser, ISavingsGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<SavingsAccountWriteResult> CreateAsync(SavingsAccount account, CancellationToken cancellationToken = default)
    {
        if (account.SavingsProductId.HasValue && !await _db.SavingsProducts.AnyAsync(p => p.Id == account.SavingsProductId, cancellationToken))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.ProductNotFound);
        }

        account.Status = SavingsAccountStatus.Pending;
        account.CreatedById = _currentUser.UserId;
        account.CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var attempt = 0; attempt < MaxAccountNumberRetries; attempt++)
        {
            account.AccountNumber = await NextAccountNumberAsync(cancellationToken);

            try
            {
                _db.Savings.Add(account);
                await _db.SaveChangesAsync(cancellationToken);
                return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.Success, account);
            }
            catch (DbUpdateException)
            {
                _db.Entry(account).State = EntityState.Detached;
            }
        }

        return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.AccountNumberGenerationFailed);
    }

    public async Task<SavingsAccountWriteResult> ApproveAsync(int id, decimal? openingBalance, decimal? overdraftLimit, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (account is null)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanApprove(account.Status))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.InvalidTransition);
        }

        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == account.SavingsProductId, cancellationToken);
        if (product is null)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.ProductNotFound);
        }

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveOpeningBalance = openingBalance ?? product.OpeningBalance ?? 0m;

        account.Status = SavingsAccountStatus.Approved;
        account.ApprovedById = _currentUser.UserId;
        account.ApprovedDate = effectiveDate;
        account.ApprovedNotes = notes;

        // Copy-at-approval, same pattern as LoanApplicationService.ApproveAsync (Phase 4).
        account.InterestRate = product.InterestRate;
        account.MinimumBalance = product.MinimumBalance;
        account.AllowOverdraft = product.AllowOverdraft;
        account.OverdraftLimit = overdraftLimit;
        account.InterestCompoundingPeriod = product.InterestCompoundingPeriod;
        account.InterestPostingPeriod = product.InterestPostingPeriod;
        account.YearDays = product.YearDays;

        account.OpeningBalance = effectiveOpeningBalance;
        account.Balance = effectiveOpeningBalance;
        account.Deposits = effectiveOpeningBalance;
        account.StartInterestCalculationDate = effectiveDate;
        account.LastInterestCalculationDate = effectiveDate;
        account.LastInterestPostingDate = effectiveDate;
        account.NextInterestCalculationDate = SavingsPeriodStepper.AddCompoundingPeriod(effectiveDate, account.InterestCompoundingPeriod);
        account.NextInterestPostingDate = SavingsPeriodStepper.AddPostingPeriod(effectiveDate, account.InterestPostingPeriod);

        if (effectiveOpeningBalance > 0)
        {
            var transaction = new SavingsTransaction
            {
                Savings = account,
                OfficeId = account.OfficeId,
                TransactionType = SavingsTransactionType.Deposit,
                Amount = effectiveOpeningBalance,
                Credit = effectiveOpeningBalance,
                Balance = effectiveOpeningBalance,
                Date = effectiveDate,
                Status = SavingsTransactionStatus.Approved,
                Reversible = false,
                CreatedById = _currentUser.UserId,
                Notes = "Opening balance",
            };
            _db.SavingsTransactions.Add(transaction);

            await _db.SaveChangesAsync(cancellationToken);
            await _glPostingService.PostDepositAsync(account, transaction, cancellationToken);
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.Success, account);
    }

    public async Task<SavingsAccountWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.ReasonRequired);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (account is null)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanDecline(account.Status))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.InvalidTransition);
        }

        account.Status = SavingsAccountStatus.Declined;
        account.DeclinedById = _currentUser.UserId;
        account.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        account.DeclinedNotes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.Success, account);
    }

    public async Task<SavingsAccountWriteResult> CloseAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (account is null)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanClose(account.Status))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.InvalidTransition);
        }

        if ((account.Balance ?? 0m) != 0m)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NonZeroBalance, account);
        }

        account.Status = SavingsAccountStatus.Closed;
        account.ClosedById = _currentUser.UserId;
        account.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        account.ClosedNotes = notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.Success, account);
    }

    public async Task<SavingsAccountWriteResult> WithdrawAccountAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (account is null)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NotFound);
        }

        if (!SavingsTransitionRules.CanWithdrawAccount(account.Status))
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.InvalidTransition);
        }

        if ((account.Balance ?? 0m) != 0m)
        {
            return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.NonZeroBalance, account);
        }

        account.Status = SavingsAccountStatus.Withdrawn;
        account.ClosedById = _currentUser.UserId;
        account.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        account.ClosedNotes = notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsAccountWriteResult(SavingsAccountWriteOutcome.Success, account);
    }

    private async Task<string> NextAccountNumberAsync(CancellationToken cancellationToken)
    {
        var accountNumbers = await _db.Savings.Select(s => s.AccountNumber).ToListAsync(cancellationToken);

        long maxSequence = 0;
        foreach (var accountNumber in accountNumbers)
        {
            if (SavingsAccountNumberFormat.TryParseSequence(accountNumber, out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return SavingsAccountNumberFormat.Format(maxSequence + 1);
    }
}
