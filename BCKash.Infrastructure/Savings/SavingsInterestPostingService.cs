using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

/// <summary>See ISavingsInterestPostingService's doc comment for the "no scheduler exists" caveat.</summary>
public class SavingsInterestPostingService : ISavingsInterestPostingService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISavingsGlPostingService _glPostingService;

    public SavingsInterestPostingService(BCKashDbContext db, ICurrentUserContext currentUser, ISavingsGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<SavingsInterestAccrualResult> AccrueAsync(int accountId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == accountId, cancellationToken);
        if (account is null || account.Status != SavingsAccountStatus.Approved)
        {
            return new SavingsInterestAccrualResult(false, 0m);
        }

        var last = account.LastInterestCalculationDate;
        if (last is null || account.NextInterestCalculationDate is not { } next || next > asOfDate || last >= asOfDate)
        {
            return new SavingsInterestAccrualResult(false, 0m);
        }

        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == account.SavingsProductId, cancellationToken);

        var (openingBalance, dailyBalances) = await BuildDailyBalancesAsync(account, last.Value, asOfDate, cancellationToken);
        var closingBalance = dailyBalances.Count > 0 ? dailyBalances[^1].Balance : openingBalance;
        var yearDays = account.YearDays == SavingsYearDays.Days360 ? 360 : 365;
        var rate = account.InterestRate ?? 0m;

        var interest = product?.InterestCalculationType == InterestCalculationType.Average
            ? SavingsInterestCalculator.CalculateAverageBalance(openingBalance, closingBalance, asOfDate.DayNumber - last.Value.DayNumber, rate, yearDays)
            : SavingsInterestCalculator.CalculateDailyBalance(dailyBalances.Select(d => (d.Date, d.Balance)).ToList(), rate, yearDays);

        account.InterestEarned = (account.InterestEarned ?? 0m) + interest;
        account.LastInterestCalculationDate = asOfDate;
        account.NextInterestCalculationDate = SavingsPeriodStepper.AddCompoundingPeriod(asOfDate, account.InterestCompoundingPeriod);

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsInterestAccrualResult(true, interest);
    }

    public async Task<SavingsInterestPostingResult> PostAsync(int accountId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var account = await _db.Savings.FirstOrDefaultAsync(s => s.Id == accountId, cancellationToken);
        if (account is null || account.Status != SavingsAccountStatus.Approved)
        {
            return new SavingsInterestPostingResult(false, 0m);
        }

        if (account.NextInterestPostingDate is not { } next || next > asOfDate)
        {
            return new SavingsInterestPostingResult(false, 0m);
        }

        var toPost = (account.InterestEarned ?? 0m) - (account.InterestPosted ?? 0m);

        if (toPost > 0)
        {
            var newBalance = (account.Balance ?? 0m) + toPost;
            var transaction = new SavingsTransaction
            {
                Savings = account,
                OfficeId = account.OfficeId,
                TransactionType = SavingsTransactionType.Interest,
                Amount = toPost,
                Credit = toPost,
                Balance = newBalance,
                Date = asOfDate,
                Status = SavingsTransactionStatus.Approved,
                Reversible = false,
                SystemInterest = true,
                CreatedById = _currentUser.UserId,
                Notes = "Interest posting",
            };
            _db.SavingsTransactions.Add(transaction);

            account.Balance = newBalance;
            account.InterestPosted = account.InterestEarned;
            account.LastInterestPostingDate = asOfDate;
            account.NextInterestPostingDate = SavingsPeriodStepper.AddPostingPeriod(asOfDate, account.InterestPostingPeriod);

            await _db.SaveChangesAsync(cancellationToken);
            await _glPostingService.PostInterestAsync(account, transaction, cancellationToken);

            return new SavingsInterestPostingResult(true, toPost);
        }

        account.LastInterestPostingDate = asOfDate;
        account.NextInterestPostingDate = SavingsPeriodStepper.AddPostingPeriod(asOfDate, account.InterestPostingPeriod);
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsInterestPostingResult(false, 0m);
    }

    public async Task RunDueAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var effectiveDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var accountIds = await _db.Savings
            .Where(s => s.Status == SavingsAccountStatus.Approved)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var accountId in accountIds)
        {
            await AccrueAsync(accountId, effectiveDate, cancellationToken);
            await PostAsync(accountId, effectiveDate, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the closing balance for each day in (from, to] by starting from the account's
    /// balance as of the end of <paramref name="from"/> (derived by subtracting every
    /// transaction dated after it back out of the current balance — safe as long as
    /// <paramref name="to"/> isn't earlier than the latest transaction, true for every caller
    /// here) and walking forward, applying each day's net movement.
    /// </summary>
    private async Task<(decimal OpeningBalance, List<(DateOnly Date, decimal Balance)> DailyBalances)> BuildDailyBalancesAsync(
        SavingsAccount account, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var transactions = await _db.SavingsTransactions
            .Where(t => t.SavingsId == account.Id && !t.Reversed && t.Date > from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var netMovement = transactions.Sum(t => (t.Credit ?? 0m) - (t.Debit ?? 0m));
        var openingBalance = (account.Balance ?? 0m) - netMovement;

        var byDate = transactions
            .GroupBy(t => t.Date!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(t => (t.Credit ?? 0m) - (t.Debit ?? 0m)));

        var dailyBalances = new List<(DateOnly Date, decimal Balance)>();
        var running = openingBalance;
        for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
        {
            if (byDate.TryGetValue(day, out var movement))
            {
                running += movement;
            }

            dailyBalances.Add((day, running));
        }

        return (openingBalance, dailyBalances);
    }
}
