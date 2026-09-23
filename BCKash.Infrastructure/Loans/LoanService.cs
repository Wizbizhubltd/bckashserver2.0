using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanService : ILoanService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILoanGlPostingService _glPostingService;

    public LoanService(BCKashDbContext db, ICurrentUserContext currentUser, ILoanGlPostingService glPostingService)
    {
        _db = db;
        _currentUser = currentUser;
        _glPostingService = glPostingService;
    }

    public async Task<LoanWriteResult> RequestChangesAsync(int id, string notes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return new LoanWriteResult(LoanWriteOutcome.ReasonRequired);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
        {
            return new LoanWriteResult(LoanWriteOutcome.NotFound);
        }

        if (!LoanTransitionRules.CanRequestChanges(loan.Status))
        {
            return new LoanWriteResult(LoanWriteOutcome.InvalidTransition);
        }

        loan.Status = LoanStatus.NeedChanges;
        loan.NeedChangesById = _currentUser.UserId;
        loan.NeedChangesDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // The legacy schema has NeedChangesById/NeedChangesDate but no NeedChangesNotes column
        // (unlike every other transition, which has a matching *Notes field) — the generic Notes
        // field is the only place to record why changes were requested.
        loan.Notes = notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanWriteResult(LoanWriteOutcome.Success, loan);
    }

    public async Task<LoanWriteResult> ResubmitAsync(int id, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
        {
            return new LoanWriteResult(LoanWriteOutcome.NotFound);
        }

        if (!LoanTransitionRules.CanResubmit(loan.Status))
        {
            return new LoanWriteResult(LoanWriteOutcome.InvalidTransition);
        }

        loan.Status = LoanStatus.Pending;

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanWriteResult(LoanWriteOutcome.Success, loan);
    }

    /// <summary>
    /// FR-LN-8 to FR-LN-11's disbursement workflow: records the transition (date, disbursed
    /// amount, notes), generates the full repayment schedule via
    /// <see cref="LoanScheduleGenerator"/>, and records a Disbursement transaction. The schedule
    /// generator is a <b>best-effort, unvalidated-against-legacy</b> implementation of standard
    /// microfinance amortization math — see docs/interest-calculation-spec.md; FR-LN-15 (the real
    /// legacy formula) remains formally unresolved. FR-LN-9's GL posting is now real (Phase 6) —
    /// see docs/gl-posting-spec.md, itself carrying the same "not validated against legacy"
    /// caveat; FR-LN-10's automatic disbursement-charge application is not implemented (charges
    /// are attached manually — see LoanChargesController).
    /// </summary>
    public async Task<LoanWriteResult> DisburseAsync(int id, DateOnly? disbursementDate, decimal disbursedAmount, string? notes, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
        {
            return new LoanWriteResult(LoanWriteOutcome.NotFound);
        }

        if (!LoanTransitionRules.CanDisburse(loan.Status))
        {
            return new LoanWriteResult(LoanWriteOutcome.InvalidTransition);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == loan.LoanProductId, cancellationToken);
        if (product is null)
        {
            return new LoanWriteResult(LoanWriteOutcome.InvalidTransition);
        }

        loan.Status = LoanStatus.Disbursed;
        loan.Principal = disbursedAmount;
        var effectiveDisbursementDate = disbursementDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        loan.DisbursementDate = effectiveDisbursementDate;
        loan.DisbursedById = _currentUser.UserId;
        loan.DisbursedNotes = notes;

        var scheduleInput = new ScheduleGenerationInput(
            Principal: disbursedAmount,
            InterestRate: loan.InterestRate ?? 0m,
            InterestRateType: loan.InterestRateType ?? InterestRateFrequencyType.Year,
            LoanTerm: loan.LoanTerm ?? 0,
            LoanTermType: loan.LoanTermType ?? FrequencyType.Months,
            RepaymentFrequency: loan.RepaymentFrequency ?? 1,
            RepaymentFrequencyType: loan.RepaymentFrequencyType ?? FrequencyType.Months,
            InterestMethod: loan.InterestMethod ?? LoanInterestMethod.Flat,
            AmortizationMethod: loan.AmortizationMethod ?? LoanAmortizationMethod.EqualInstallment,
            CalculationPeriodType: product.InterestCalculationPeriodType,
            YearDays: product.YearDays,
            MonthDays: product.MonthDays,
            GraceOnPrincipal: loan.GraceOnPrincipal ?? 0,
            GraceOnInterestCharged: loan.GraceOnInterestCharged ?? 0,
            GraceOnInterestPayment: loan.GraceOnInterestPayment ?? 0,
            DisbursementDate: effectiveDisbursementDate);

        var installments = LoanScheduleGenerator.Generate(scheduleInput);
        foreach (var installment in installments)
        {
            var totalDue = installment.Principal + installment.Interest;
            _db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Loan = loan,
                Installment = installment.Number,
                DueDate = installment.DueDate,
                Principal = installment.Principal,
                Interest = installment.Interest,
                TotalDue = totalDue,
                PrincipalPaid = 0m,
                InterestPaid = 0m,
                FeesPaid = 0m,
                PenaltyPaid = 0m,
                Paid = false,
                CreatedById = _currentUser.UserId,
            });
        }

        var disbursementTransaction = new LoanTransaction
        {
            Loan = loan,
            OfficeId = loan.OfficeId,
            ClientId = loan.ClientId,
            TransactionType = LoanTransactionType.Disbursement,
            Amount = disbursedAmount,
            Debit = disbursedAmount,
            Date = effectiveDisbursementDate,
            Status = ApprovalStatus.Approved,
            Reversible = false,
            CreatedById = _currentUser.UserId,
            Notes = notes,
        };
        _db.LoanTransactions.Add(disbursementTransaction);

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostDisbursementAsync(loan, disbursementTransaction, cancellationToken);

        return new LoanWriteResult(LoanWriteOutcome.Success, loan);
    }
}
