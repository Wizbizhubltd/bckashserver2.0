using BCKash.Application.GeneralLedger;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// FR-LN-16/FR-LN-18. Overpayment (FR-LN-19) is detected and recorded on the transaction but not
/// auto-applied to future installments across separate transactions — that needs a running
/// credit ledger this pass doesn't build; the product's `AllocateOverpayments` flag is read only
/// to label the response, not to move money. Reversal is a direct undo of the specific
/// transaction's own schedule-mapping allocations, correct for the common case (reversing the
/// most recent repayment) but not a full replay-based recomputation if reversals happen out of
/// chronological order.
/// </summary>
public class LoanRepaymentService : ILoanRepaymentService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILoanNpaService _npaService;
    private readonly ILoanGlPostingService _glPostingService;
    private readonly IGlJournalEntryService _glJournalEntryService;

    public LoanRepaymentService(
        BCKashDbContext db,
        ICurrentUserContext currentUser,
        ILoanNpaService npaService,
        ILoanGlPostingService glPostingService,
        IGlJournalEntryService glJournalEntryService)
    {
        _db = db;
        _currentUser = currentUser;
        _npaService = npaService;
        _glPostingService = glPostingService;
        _glJournalEntryService = glJournalEntryService;
    }

    public async Task<LoanRepaymentWriteResult> RecordRepaymentAsync(int loanId, decimal amount, int? paymentTypeId, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.InvalidAmount);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.NotFound);
        }

        if (!LoanTransitionRules.HasActiveSchedule(loan.Status))
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.InvalidLoanStatus);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        var strategy = product?.LoanTransactionStrategy ?? LoanTransactionStrategy.InterestPrincipalPenaltyFees;

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
        var transaction = new LoanTransaction
        {
            Loan = loan,
            OfficeId = loan.OfficeId,
            ClientId = loan.ClientId,
            PaymentTypeId = paymentTypeId,
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
            Reversible = true,
            CreatedById = _currentUser.UserId,
            Notes = notes,
        };
        _db.LoanTransactions.Add(transaction);

        var schedulesById = unpaidSchedules.ToDictionary(s => s.Id);
        foreach (var allocation in allocationResult.Allocations)
        {
            var schedule = schedulesById[allocation.ScheduleId];

            _db.LoanTransactionRepaymentScheduleMappings.Add(new LoanTransactionRepaymentScheduleMapping
            {
                LoanTransaction = transaction,
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

            if (schedule.Paid)
            {
                var totalDue = schedule.TotalDue ?? (schedule.Principal ?? 0m) + (schedule.Interest ?? 0m) + (schedule.Fees ?? 0m) + (schedule.Penalty ?? 0m);
                var totalPaid = (schedule.PrincipalPaid ?? 0m) + (schedule.InterestPaid ?? 0m) + (schedule.FeesPaid ?? 0m) + (schedule.PenaltyPaid ?? 0m);
                if (schedule.DueDate.HasValue && effectiveDate <= schedule.DueDate.Value)
                {
                    schedule.TotalPaidAdvance = totalPaid;
                }
                else
                {
                    schedule.TotalPaidLate = totalPaid;
                }
                _ = totalDue; // kept for clarity of intent; TotalDue itself was already set at schedule generation
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _npaService.RecomputeAsync(loanId, cancellationToken);
        await _glPostingService.PostRepaymentAsync(loan, transaction, cancellationToken);

        return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.Success, transaction, allocationResult.Overpayment);
    }

    public async Task<LoanRepaymentWriteResult> ReverseAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.LoanTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (transaction is null)
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.TransactionNotFound);
        }

        if (transaction.Reversed)
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.AlreadyReversed);
        }

        if (!transaction.Reversible)
        {
            return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.NotReversible);
        }

        var mappings = await _db.LoanTransactionRepaymentScheduleMappings
            .Where(m => m.LoanTransactionId == transactionId)
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            var schedule = await _db.LoanRepaymentSchedules.FirstOrDefaultAsync(s => s.Id == mapping.LoanRepaymentScheduleId, cancellationToken);
            if (schedule is null)
            {
                continue;
            }

            schedule.PrincipalPaid = Math.Max(0m, (schedule.PrincipalPaid ?? 0m) - (mapping.Principal ?? 0m));
            schedule.InterestPaid = Math.Max(0m, (schedule.InterestPaid ?? 0m) - (mapping.Interest ?? 0m));
            schedule.FeesPaid = Math.Max(0m, (schedule.FeesPaid ?? 0m) - (mapping.Fee ?? 0m));
            schedule.PenaltyPaid = Math.Max(0m, (schedule.PenaltyPaid ?? 0m) - (mapping.Penalty ?? 0m));
            schedule.Paid = false;
            schedule.ModifiedById = _currentUser.UserId;
        }

        transaction.Reversed = true;
        transaction.ReversalType = LoanTransactionReversalType.User;
        transaction.ModifiedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _glJournalEntryService.ReverseByReferenceAsync($"LOAN-{transaction.TransactionType}-{transaction.Id}", cancellationToken);

        if (transaction.LoanId.HasValue)
        {
            await _npaService.RecomputeAsync(transaction.LoanId.Value, cancellationToken);
        }

        return new LoanRepaymentWriteResult(LoanRepaymentWriteOutcome.Success, transaction);
    }

    private static decimal Outstanding(decimal? due, decimal? waived, decimal? writtenOff, decimal? paid) =>
        Math.Max(0m, (due ?? 0m) - (waived ?? 0m) - (writtenOff ?? 0m) - (paid ?? 0m));
}
