using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

public class SavingsChargeService : ISavingsChargeService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISavingsGlPostingService _glPostingService;

    public SavingsChargeService(BCKashDbContext db, ICurrentUserContext currentUser, ISavingsGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<SavingsChargeWriteResult> AttachAsync(int accountId, SavingsChargeType chargeType, bool penalty, decimal amount, DateOnly? dueDate, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.InvalidAmount);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == accountId, cancellationToken);
        if (account is null)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.NotFound);
        }

        var charge = new SavingsCharge
        {
            Savings = account,
            ChargeType = chargeType,
            ChargeOption = SavingsChargeOption.Flat,
            Penalty = penalty,
            Amount = amount,
            DueDate = dueDate,
        };
        _db.SavingsCharges.Add(charge);

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.Success, charge);
    }

    public async Task<SavingsChargeWriteResult> WaiveAsync(int savingsChargeId, CancellationToken cancellationToken = default)
    {
        var charge = await _db.SavingsCharges.FirstOrDefaultAsync(c => c.Id == savingsChargeId, cancellationToken);
        if (charge is null)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.ChargeNotFound);
        }

        if (charge.Waived)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.AlreadyWaived);
        }

        charge.Waived = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.Success, charge);
    }

    public async Task<SavingsChargeWriteResult> PayDueAsync(int savingsChargeId, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var charge = await _db.SavingsCharges.FirstOrDefaultAsync(c => c.Id == savingsChargeId, cancellationToken);
        if (charge is null)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.ChargeNotFound);
        }

        if (charge.Waived)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.AlreadyWaived);
        }

        if ((charge.AmountPaid ?? 0m) > 0)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.AlreadyPaid);
        }

        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == charge.SavingsId, cancellationToken);
        if (account is null)
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.NotFound);
        }

        var amount = charge.Amount ?? 0m;
        var floor = SavingsBalanceRules.EffectiveFloor(account.AllowOverdraft, account.MinimumBalance, account.OverdraftLimit);
        if (!SavingsBalanceRules.CanWithdraw(account.Balance ?? 0m, amount, floor))
        {
            return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.InsufficientBalance);
        }

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var newBalance = (account.Balance ?? 0m) - amount;

        var transaction = new SavingsTransaction
        {
            Savings = account,
            OfficeId = account.OfficeId,
            TransactionType = SavingsTransactionType.FeesPayment,
            Amount = amount,
            Debit = amount,
            Balance = newBalance,
            Date = effectiveDate,
            Status = SavingsTransactionStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = $"{charge.ChargeType} charge payment",
        };
        _db.SavingsTransactions.Add(transaction);

        account.Balance = newBalance;
        account.Fees = (account.Fees ?? 0m) + (charge.Penalty ? 0m : amount);
        account.Penalty = (account.Penalty ?? 0m) + (charge.Penalty ? amount : 0m);
        account.ModifiedById = _currentUser.UserId;
        account.ModifiedDate = effectiveDate;

        charge.AmountPaid = amount;

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostChargeAsync(account, transaction, charge.Penalty, cancellationToken);

        return new SavingsChargeWriteResult(SavingsChargeWriteOutcome.Success, charge);
    }
}
