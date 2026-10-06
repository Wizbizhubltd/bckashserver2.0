using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// The daily automatic-penalty run (Settings → Loan → "Apply penalties automatically").
/// <list type="bullet">
/// <item>Late repayment fee (OverdueInstallmentFee): per instalment still owing principal or interest,
/// once its due date plus grace has passed; a percentage is taken of that instalment only.</item>
/// <item>Loan default penalty (OverdueMaturity): per loan still owing principal, once its final
/// repayment date plus grace has passed; added to the last instalment.</item>
/// </list>
/// Grace is the penalty's own grace days, or else the matching overdue threshold from the settings.
/// Which penalties apply: those attached to the loan's product, or — when its product has none —
/// every active one. Each charge is written to the instalment (so the next repayment collects it, in
/// the product's allocation order), as a loan charge (visible and waivable), as a loan transaction
/// (statement trail), and as a <see cref="LoanPenaltyApplication"/> that makes the run idempotent.
/// </summary>
public class LoanPenaltyService : ILoanPenaltyService
{
    // Loans are processed and saved in batches so one bad loan can't sink the whole run, and the
    // change tracker is emptied after each one so memory stays at one batch, not the whole loan book.
    // The bulk reads skip the Redis second-level cache (NotCacheable): it would buffer every row a
    // second time and push the whole result to Redis, for a query that runs once a day.
    private const int BatchSize = 200;

    private readonly BCKashDbContext _db;
    private readonly IOverdueRulesProvider _rules;
    private readonly ILogger<LoanPenaltyService> _logger;

    public LoanPenaltyService(BCKashDbContext db, IOverdueRulesProvider rules, ILogger<LoanPenaltyService> logger)
    {
        _db = db;
        _rules = rules;
        _logger = logger;
    }

    public async Task<PenaltyRunResult> RunDueAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rules = await _rules.GetAsync(cancellationToken);
        if (!rules.AutoApplyPenalty)
        {
            return new PenaltyRunResult(0, 0, 0, Skipped: true);
        }

        var penalties = await _db.Charges
            .Where(c => c.Active && c.Product == ChargeProduct.Loan
                     && (c.ChargeType == ChargeType.OverdueInstallmentFee || c.ChargeType == ChargeType.OverdueMaturity)
                     && c.Amount > 0)
            .ToListAsync(cancellationToken);
        if (penalties.Count == 0)
        {
            return new PenaltyRunResult(0, 0, 0, Skipped: false);
        }

        var penaltyIds = penalties.Select(p => p.Id).ToList();
        var productPenalties = (await _db.LoanProductCharges
                .Where(pc => pc.LoanProductId.HasValue && pc.ChargeId.HasValue && penaltyIds.Contains(pc.ChargeId.Value))
                .Select(pc => new { ProductId = pc.LoanProductId!.Value, ChargeId = pc.ChargeId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ChargeId).ToHashSet());

        // Disbursed loans with something overdue at all — the per-penalty grace is applied below.
        var candidateLoanIds = await _db.LoanRepaymentSchedules
            .NotCacheable()
            .Where(s => s.DueDate < today
                     && ((s.Principal ?? 0) > (s.PrincipalPaid ?? 0) || (s.Interest ?? 0) > (s.InterestPaid ?? 0))
                     && s.Loan!.Status == LoanStatus.Disbursed)
            .Select(s => s.LoanId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        int loansCharged = 0, penaltiesCharged = 0;
        decimal totalCharged = 0;
        foreach (var batch in candidateLoanIds.Chunk(BatchSize))
        {
            try
            {
                var (loans, count, amount) = await ChargeBatchAsync(batch, penalties, productPenalties, rules, today, cancellationToken);
                loansCharged += loans;
                penaltiesCharged += count;
                totalCharged += amount;
            }
            catch (DbUpdateException ex)
            {
                // Most likely another run charged the same occurrence first (unique index) — the next
                // run picks up whatever this batch didn't write.
                _logger.LogWarning(ex, "Penalty batch starting at loan {LoanId} was not saved.", batch[0]);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation("Automatic penalties for {Date}: {Count} charged on {Loans} loans, ₦{Total}.", today, penaltiesCharged, loansCharged, totalCharged);
        return new PenaltyRunResult(loansCharged, penaltiesCharged, totalCharged, Skipped: false);
    }

    private async Task<(int Loans, int Count, decimal Amount)> ChargeBatchAsync(
        int[] loanIds, List<Charge> penalties, Dictionary<int, HashSet<int>> productPenalties, OverdueRules rules, DateOnly today, CancellationToken cancellationToken)
    {
        var loans = await _db.Loans.AsNoTracking().NotCacheable().Where(l => loanIds.Contains(l.Id)).ToListAsync(cancellationToken);
        // Tracked: the penalties are written onto these instalments.
        var schedules = (await _db.LoanRepaymentSchedules.NotCacheable().Where(s => s.LoanId.HasValue && loanIds.Contains(s.LoanId.Value)).ToListAsync(cancellationToken))
            .GroupBy(s => s.LoanId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.DueDate).ToList());
        var applied = await _db.LoanPenaltyApplications.AsNoTracking().NotCacheable().Where(a => loanIds.Contains(a.LoanId)).ToListAsync(cancellationToken);
        var history = applied.ToLookup(a => (a.LoanId, a.ChargeId, a.ScheduleId));
        // What each penalty has charged on each loan so far, kept up to date as this batch charges more.
        var chargedSoFar = applied.GroupBy(a => (a.LoanId, a.ChargeId)).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var pending = new List<(LoanPenaltyApplication Record, LoanCharge Charge, LoanTransaction Transaction)>();
        var chargedLoans = new HashSet<int>();

        foreach (var loan in loans)
        {
            if (!schedules.TryGetValue(loan.Id, out var installments) || installments.Count == 0)
            {
                continue;
            }

            var applicable = loan.LoanProductId is { } productId && productPenalties.TryGetValue(productId, out var attached)
                ? penalties.Where(p => attached.Contains(p.Id))
                : penalties;
            var disbursed = loan.Principal ?? loan.ApprovedAmount ?? 0;

            foreach (var penalty in applicable)
            {
                if (penalty.ChargeType == ChargeType.OverdueInstallmentFee)
                {
                    var rule = ToRule(penalty, penalty.GraceDays ?? rules.RepaymentOverdueDays);
                    foreach (var installment in installments.Where(s => s.DueDate < today))
                    {
                        var principal = Outstanding(installment.Principal, installment.PrincipalWaived, installment.PrincipalWrittenOff, installment.PrincipalPaid);
                        var interest = Outstanding(installment.Interest, installment.InterestWaived, installment.InterestWrittenOff, installment.InterestPaid);
                        if (principal + interest <= 0)
                        {
                            continue;
                        }

                        var fees = Outstanding(installment.Fees, installment.FeesWaived, installment.FeesWrittenOff, installment.FeesPaid);
                        var baseAmount = penalty.ChargeOption switch
                        {
                            ChargeOption.InstallmentPrincipalDue => principal,
                            ChargeOption.InstallmentInterestDue => interest,
                            ChargeOption.InstallmentPrincipalInterestDue => principal + interest,
                            // Excludes penalties already on the instalment, so penalties never compound.
                            _ => principal + interest + fees,
                        };

                        Charge(loan, penalty, rule, installment, installment.Id, installment.DueDate!.Value, baseAmount, disbursed);
                    }
                }
                else
                {
                    var finalDate = loan.ExpectedMaturityDate ?? installments.Max(s => s.DueDate);
                    if (finalDate is not { } maturity || maturity >= today)
                    {
                        continue;
                    }

                    var principal = installments.Sum(s => Outstanding(s.Principal, s.PrincipalWaived, s.PrincipalWrittenOff, s.PrincipalPaid));
                    if (principal <= 0)
                    {
                        continue;
                    }

                    var interest = installments.Sum(s => Outstanding(s.Interest, s.InterestWaived, s.InterestWrittenOff, s.InterestPaid));
                    var fees = installments.Sum(s => Outstanding(s.Fees, s.FeesWaived, s.FeesWrittenOff, s.FeesPaid));
                    var penaltiesOwed = installments.Sum(s => Outstanding(s.Penalty, s.PenaltyWaived, s.PenaltyWrittenOff, s.PenaltyPaid));
                    var baseAmount = penalty.ChargeOption switch
                    {
                        ChargeOption.PrincipalDue => principal,
                        ChargeOption.TotalDue => principal + interest + fees,
                        _ => principal + interest + fees + penaltiesOwed, // TotalOutstanding — bounded by the penalty's cap
                    };

                    var rule = ToRule(penalty, penalty.GraceDays ?? rules.LoanOverdueDays);
                    Charge(loan, penalty, rule, installments[^1], 0, maturity, baseAmount, disbursed);
                }
            }
        }

        if (pending.Count == 0)
        {
            return (0, 0, 0);
        }

        // Charges and transactions first, so their ids can go on the idempotency records.
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var (record, charge, transaction) in pending)
        {
            record.LoanChargeId = charge.Id;
            record.LoanTransactionId = transaction.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (chargedLoans.Count, pending.Count, pending.Sum(p => p.Record.Amount));

        void Charge(Loan loan, Charge penalty, PenaltyRule rule, LoanRepaymentSchedule target, int scheduleId, DateOnly triggerDate, decimal baseAmount, decimal disbursed)
        {
            var previous = history[(loan.Id, penalty.Id, scheduleId)];
            // The cap covers everything this penalty has charged on the loan, across instalments.
            var chargedOnLoan = chargedSoFar.GetValueOrDefault((loan.Id, penalty.Id));

            var due = LoanPenaltyCalculator.Due(rule, triggerDate, today, previous.Select(a => a.Occurrence).ToHashSet(), chargedOnLoan, baseAmount, disbursed);
            foreach (var occurrence in due)
            {
                target.Penalty = (target.Penalty ?? 0) + occurrence.Amount;
                if (target.TotalDue.HasValue)
                {
                    target.TotalDue += occurrence.Amount;
                }

                target.Paid = false;

                var loanCharge = new LoanCharge
                {
                    LoanId = loan.Id,
                    ChargeId = penalty.Id,
                    Penalty = true,
                    ChargeType = penalty.ChargeType == ChargeType.OverdueMaturity ? LoanChargeType.OverdueMaturity : LoanChargeType.OverdueInstallmentFee,
                    ChargeOption = ToLoanChargeOption(penalty.ChargeOption),
                    Amount = occurrence.Amount,
                    AmountPaid = 0,
                    DueDate = occurrence.Date,
                    GracePeriod = rule.GraceDays,
                };
                var transaction = new LoanTransaction
                {
                    LoanId = loan.Id,
                    OfficeId = loan.OfficeId,
                    ClientId = loan.ClientId,
                    ChargeId = penalty.Id,
                    LoanRepaymentScheduleId = target.Id,
                    TransactionType = penalty.ChargeType == ChargeType.OverdueMaturity ? LoanTransactionType.OverdueMaturity : LoanTransactionType.OverdueInstallmentFee,
                    Amount = occurrence.Amount,
                    Penalty = occurrence.Amount,
                    Debit = occurrence.Amount,
                    Date = occurrence.Date,
                    Status = ApprovalStatus.Approved,
                    ApprovedDate = today,
                    Reversible = false,
                    Notes = $"{penalty.Name} (automatic, occurrence {occurrence.Occurrence})",
                };
                var record = new LoanPenaltyApplication
                {
                    LoanId = loan.Id,
                    ChargeId = penalty.Id,
                    ScheduleId = scheduleId,
                    Occurrence = occurrence.Occurrence,
                    DueDate = occurrence.Date,
                    Amount = occurrence.Amount,
                    CreatedAtUtc = DateTime.UtcNow,
                };

                _db.LoanCharges.Add(loanCharge);
                _db.LoanTransactions.Add(transaction);
                _db.LoanPenaltyApplications.Add(record);
                pending.Add((record, loanCharge, transaction));
                chargedLoans.Add(loan.Id);
                chargedSoFar[(loan.Id, penalty.Id)] = chargedSoFar.GetValueOrDefault((loan.Id, penalty.Id)) + occurrence.Amount;
            }
        }
    }

    private static PenaltyRule ToRule(Charge c, int graceDays) =>
        new(c.Id, c.ChargeOption, c.Amount ?? 0, c.MinimumAmount, c.MaximumAmount, graceDays, c.RepeatEveryDays, c.MaxTotalPercent);

    private static LoanChargeCalculationType ToLoanChargeOption(ChargeOption option) => option switch
    {
        ChargeOption.Flat => LoanChargeCalculationType.Flat,
        ChargeOption.InstallmentPrincipalDue => LoanChargeCalculationType.InstallmentPrincipalDue,
        ChargeOption.InstallmentPrincipalInterestDue => LoanChargeCalculationType.InstallmentPrincipalInterestDue,
        ChargeOption.InstallmentInterestDue => LoanChargeCalculationType.InstallmentInterestDue,
        ChargeOption.InstallmentTotalDue => LoanChargeCalculationType.InstallmentTotalDue,
        ChargeOption.TotalDue => LoanChargeCalculationType.TotalDue,
        ChargeOption.OriginalPrincipal => LoanChargeCalculationType.OriginalPrincipal,
        _ => LoanChargeCalculationType.Percentage,
    };

    private static decimal Outstanding(decimal? amount, decimal? waived, decimal? writtenOff, decimal? paid) =>
        Math.Max(0, (amount ?? 0) - (waived ?? 0) - (writtenOff ?? 0) - (paid ?? 0));
}
