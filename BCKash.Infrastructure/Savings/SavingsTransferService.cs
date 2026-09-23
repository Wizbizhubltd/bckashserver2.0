using BCKash.Application.GeneralLedger;
using BCKash.Application.Loans;
using BCKash.Application.Savings;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

/// <summary>FR-SAV-6. See ISavingsTransferService's doc comment for the atomicity/GL-posting design.</summary>
public class SavingsTransferService : ISavingsTransferService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IGlClosureGuard _closureGuard;
    private readonly ILoanNpaService _npaService;

    public SavingsTransferService(BCKashDbContext db, ICurrentUserContext currentUser, IGlClosureGuard closureGuard, ILoanNpaService npaService)
    {
        _db = db;
        _currentUser = currentUser;
        _closureGuard = closureGuard;
        _npaService = npaService;
    }

    public async Task<SavingsTransferResult> RepayLoanFromSavingsAsync(int savingsId, int loanId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.InvalidAmount);
        }

        var savings = await _db.Savings.FirstOrDefaultAsync(s => s.Id == savingsId, cancellationToken);
        if (savings is null)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.SavingsNotFound);
        }

        if (!SavingsTransitionRules.CanTransact(savings.Status))
        {
            return new SavingsTransferResult(SavingsTransferOutcome.SavingsAccountNotTransactable);
        }

        var floor = SavingsBalanceRules.EffectiveFloor(savings.AllowOverdraft, savings.MinimumBalance, savings.OverdraftLimit);
        if (!SavingsBalanceRules.CanWithdraw(savings.Balance ?? 0m, amount, floor))
        {
            return new SavingsTransferResult(SavingsTransferOutcome.InsufficientBalance);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.LoanNotFound);
        }

        if (!LoanTransitionRules.HasActiveSchedule(loan.Status))
        {
            return new SavingsTransferResult(SavingsTransferOutcome.InvalidLoanStatus);
        }

        var loanProduct = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        var savingsProduct = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == savings.SavingsProductId, cancellationToken);
        var strategy = loanProduct?.LoanTransactionStrategy ?? LoanTransactionStrategy.InterestPrincipalPenaltyFees;

        var unpaidSchedules = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loanId && s.Paid == false)
            .OrderBy(s => s.DueDate)
            .ToListAsync(cancellationToken);

        var outstandings = unpaidSchedules.Select(s => new OutstandingInstallment(
            s.Id,
            s.DueDate ?? DateOnly.MinValue,
            Outstanding(s.Principal, s.PrincipalWaived, s.PrincipalWrittenOff, s.PrincipalPaid),
            Outstanding(s.Interest, s.InterestWaived, s.InterestWrittenOff, s.InterestPaid),
            Outstanding(s.Fees, s.FeesWaived, s.FeesWrittenOff, s.FeesPaid),
            Outstanding(s.Penalty, s.PenaltyWaived, s.PenaltyWrittenOff, s.PenaltyPaid)))
            .ToList();

        var allocationResult = LoanRepaymentAllocationEngine.Allocate(outstandings, amount, strategy);
        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // --- Savings side: a debit (withdrawal-shaped) transfer transaction. ---
        var newSavingsBalance = (savings.Balance ?? 0m) - amount;
        var savingsTransaction = new SavingsTransaction
        {
            Savings = savings,
            OfficeId = savings.OfficeId,
            TransactionType = SavingsTransactionType.TransferLoan,
            Amount = amount,
            Debit = amount,
            Balance = newSavingsBalance,
            Date = effectiveDate,
            Status = SavingsTransactionStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = notes ?? $"Repayment transfer to loan #{loanId}",
        };
        _db.SavingsTransactions.Add(savingsTransaction);
        savings.Balance = newSavingsBalance;
        savings.Withdrawals = (savings.Withdrawals ?? 0m) + amount;
        savings.ModifiedById = _currentUser.UserId;
        savings.ModifiedDate = effectiveDate;

        // --- Loan side: a normal repayment transaction + schedule allocation. ---
        var loanTransaction = new LoanTransaction
        {
            Loan = loan,
            OfficeId = loan.OfficeId,
            ClientId = loan.ClientId,
            TransactionType = LoanTransactionType.Repayment,
            Amount = amount,
            Credit = amount,
            Principal = allocationResult.Allocations.Sum(a => a.Principal),
            Interest = allocationResult.Allocations.Sum(a => a.Interest),
            Fee = allocationResult.Allocations.Sum(a => a.Fees),
            Penalty = allocationResult.Allocations.Sum(a => a.Penalty),
            Overpayment = allocationResult.Overpayment > 0 ? allocationResult.Overpayment : null,
            OverpaymentDerived = allocationResult.Overpayment,
            Date = effectiveDate,
            Status = ApprovalStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = notes ?? $"Repayment transfer from savings #{savingsId}",
        };
        _db.LoanTransactions.Add(loanTransaction);

        var schedulesById = unpaidSchedules.ToDictionary(s => s.Id);
        foreach (var allocation in allocationResult.Allocations)
        {
            var schedule = schedulesById[allocation.ScheduleId];

            _db.LoanTransactionRepaymentScheduleMappings.Add(new LoanTransactionRepaymentScheduleMapping
            {
                LoanTransaction = loanTransaction,
                LoanRepaymentSchedule = schedule,
                Principal = allocation.Principal,
                Interest = allocation.Interest,
                Fee = allocation.Fees,
                Penalty = allocation.Penalty,
            });

            schedule.PrincipalPaid = (schedule.PrincipalPaid ?? 0m) + allocation.Principal;
            schedule.InterestPaid = (schedule.InterestPaid ?? 0m) + allocation.Interest;
            schedule.FeesPaid = (schedule.FeesPaid ?? 0m) + allocation.Fees;
            schedule.PenaltyPaid = (schedule.PenaltyPaid ?? 0m) + allocation.Penalty;
            schedule.ModifiedById = _currentUser.UserId;

            schedule.Paid = Outstanding(schedule.Principal, schedule.PrincipalWaived, schedule.PrincipalWrittenOff, schedule.PrincipalPaid) <= 0m
                && Outstanding(schedule.Interest, schedule.InterestWaived, schedule.InterestWrittenOff, schedule.InterestPaid) <= 0m
                && Outstanding(schedule.Fees, schedule.FeesWaived, schedule.FeesWrittenOff, schedule.FeesPaid) <= 0m
                && Outstanding(schedule.Penalty, schedule.PenaltyWaived, schedule.PenaltyWrittenOff, schedule.PenaltyPaid) <= 0m;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // --- One balanced GL batch covering both sides. ---
        if (loanProduct is not null && savingsProduct?.GlAccountSavingsControlId is int savingsControl
            && await _closureGuard.IsDatePostableAsync(loan.OfficeId, effectiveDate, cancellationToken)
            && await _closureGuard.IsDatePostableAsync(savings.OfficeId, effectiveDate, cancellationToken))
        {
            var lines = LoanGlPostingRules.ForRepaymentFromSavings(
                loanProduct, savingsControl,
                loanTransaction.Principal ?? 0m, loanTransaction.Interest ?? 0m, loanTransaction.Fee ?? 0m, loanTransaction.Penalty ?? 0m, allocationResult.Overpayment);

            if (lines.Count > 0)
            {
                var reference = $"TRANSFER-REPAY-{loanTransaction.Id}";
                foreach (var line in lines)
                {
                    _db.GlJournalEntries.Add(new GlJournalEntry
                    {
                        OfficeId = loan.OfficeId,
                        GlAccountId = line.GlAccountId,
                        TransactionType = GlTransactionType.Repayment,
                        TransactionSubType = line.SubType,
                        Debit = line.Debit,
                        Credit = line.Credit,
                        Reference = reference,
                        LoanId = loan.Id,
                        LoanTransactionId = loanTransaction.Id,
                        SavingsId = savings.Id,
                        SavingsTransactionId = savingsTransaction.Id,
                        Date = effectiveDate,
                        ManualEntry = false,
                        Approved = true,
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        await _npaService.RecomputeAsync(loanId, cancellationToken);

        return new SavingsTransferResult(SavingsTransferOutcome.Success, allocationResult.Overpayment);
    }

    public async Task<SavingsTransferResult> DisburseLoanToSavingsAsync(int loanId, int savingsId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.InvalidAmount);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.LoanNotFound);
        }

        var savings = await _db.Savings.FirstOrDefaultAsync(s => s.Id == savingsId, cancellationToken);
        if (savings is null)
        {
            return new SavingsTransferResult(SavingsTransferOutcome.SavingsNotFound);
        }

        if (!SavingsTransitionRules.CanTransact(savings.Status))
        {
            return new SavingsTransferResult(SavingsTransferOutcome.SavingsAccountNotTransactable);
        }

        var loanProduct = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        var savingsProduct = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == savings.SavingsProductId, cancellationToken);

        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var newBalance = (savings.Balance ?? 0m) + amount;

        var savingsTransaction = new SavingsTransaction
        {
            Savings = savings,
            OfficeId = savings.OfficeId,
            TransactionType = SavingsTransactionType.TransferSavings,
            Amount = amount,
            Credit = amount,
            Balance = newBalance,
            Date = effectiveDate,
            Status = SavingsTransactionStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = notes ?? $"Disbursement transfer from loan #{loanId}",
        };
        _db.SavingsTransactions.Add(savingsTransaction);

        savings.Balance = newBalance;
        savings.Deposits = (savings.Deposits ?? 0m) + amount;
        savings.ModifiedById = _currentUser.UserId;
        savings.ModifiedDate = effectiveDate;

        await _db.SaveChangesAsync(cancellationToken);

        if (loanProduct?.GlAccountFundSourceId is int fundSource && savingsProduct?.GlAccountSavingsControlId is int savingsControl
            && await _closureGuard.IsDatePostableAsync(loan.OfficeId, effectiveDate, cancellationToken)
            && await _closureGuard.IsDatePostableAsync(savings.OfficeId, effectiveDate, cancellationToken))
        {
            var reference = $"TRANSFER-DISBURSE-{savingsTransaction.Id}";
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = loan.OfficeId,
                GlAccountId = fundSource,
                TransactionType = GlTransactionType.TransferFund,
                Debit = amount,
                Reference = reference,
                LoanId = loan.Id,
                SavingsId = savings.Id,
                SavingsTransactionId = savingsTransaction.Id,
                Date = effectiveDate,
                ManualEntry = false,
                Approved = true,
            });
            _db.GlJournalEntries.Add(new GlJournalEntry
            {
                OfficeId = savings.OfficeId,
                GlAccountId = savingsControl,
                TransactionType = GlTransactionType.TransferFund,
                Credit = amount,
                Reference = reference,
                LoanId = loan.Id,
                SavingsId = savings.Id,
                SavingsTransactionId = savingsTransaction.Id,
                Date = effectiveDate,
                ManualEntry = false,
                Approved = true,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        return new SavingsTransferResult(SavingsTransferOutcome.Success);
    }

    private static decimal Outstanding(decimal? due, decimal? waived, decimal? writtenOff, decimal? paid) =>
        Math.Max(0m, (due ?? 0m) - (waived ?? 0m) - (writtenOff ?? 0m) - (paid ?? 0m));
}
