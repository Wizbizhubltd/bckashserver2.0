using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanProductService : ILoanProductService
{
    private readonly BCKashDbContext _db;

    public LoanProductService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<LoanProductWriteResult> CreateAsync(LoanProduct product, CancellationToken cancellationToken = default)
    {
        if (!IsValidRange(product))
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.InvalidRange);
        }

        _db.LoanProducts.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return new LoanProductWriteResult(LoanProductWriteOutcome.Success, product);
    }

    public async Task<LoanProductWriteResult> UpdateAsync(int id, LoanProduct updated, CancellationToken cancellationToken = default)
    {
        if (!IsValidRange(updated))
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.InvalidRange);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.NotFound);
        }

        product.Name = updated.Name;
        product.ShortName = updated.ShortName;
        product.Description = updated.Description;
        product.FundId = updated.FundId;
        product.CurrencyId = updated.CurrencyId;
        product.Decimals = updated.Decimals;
        product.MinimumPrincipal = updated.MinimumPrincipal;
        product.DefaultPrincipal = updated.DefaultPrincipal;
        product.MaximumPrincipal = updated.MaximumPrincipal;
        product.MinimumLoanTerm = updated.MinimumLoanTerm;
        product.DefaultLoanTerm = updated.DefaultLoanTerm;
        product.MaximumLoanTerm = updated.MaximumLoanTerm;
        product.RepaymentFrequency = updated.RepaymentFrequency;
        product.RepaymentFrequencyType = updated.RepaymentFrequencyType;
        product.MinimumInterestRate = updated.MinimumInterestRate;
        product.DefaultInterestRate = updated.DefaultInterestRate;
        product.MaximumInterestRate = updated.MaximumInterestRate;
        product.InterestRateType = updated.InterestRateType;
        product.GraceOnInterestCharged = updated.GraceOnInterestCharged;
        product.GraceOnPrincipal = updated.GraceOnPrincipal;
        product.GraceOnInterestPayment = updated.GraceOnInterestPayment;
        product.AllowCustomGrace = updated.AllowCustomGrace;
        product.AllowStandingInstructions = updated.AllowStandingInstructions;
        product.InterestMethod = updated.InterestMethod;
        product.AmortizationMethod = updated.AmortizationMethod;
        product.InterestCalculationPeriodType = updated.InterestCalculationPeriodType;
        product.YearDays = updated.YearDays;
        product.MonthDays = updated.MonthDays;
        product.LoanTransactionStrategy = updated.LoanTransactionStrategy;
        product.IncludeInCycle = updated.IncludeInCycle;
        product.LockGuarantee = updated.LockGuarantee;
        product.AllocateOverpayments = updated.AllocateOverpayments;
        product.AllowAdditionalCharges = updated.AllowAdditionalCharges;
        product.AccountingRule = updated.AccountingRule;
        product.NpaDays = updated.NpaDays;
        product.ArrearsGraceDays = updated.ArrearsGraceDays;
        product.NpaSuspendIncome = updated.NpaSuspendIncome;
        product.GlAccountFundSourceId = updated.GlAccountFundSourceId;
        product.GlAccountLoanPortfolioId = updated.GlAccountLoanPortfolioId;
        product.GlAccountReceivableInterestId = updated.GlAccountReceivableInterestId;
        product.GlAccountReceivableFeeId = updated.GlAccountReceivableFeeId;
        product.GlAccountReceivablePenaltyId = updated.GlAccountReceivablePenaltyId;
        product.GlAccountLoanOverPaymentsId = updated.GlAccountLoanOverPaymentsId;
        product.GlAccountSuspendedIncomeId = updated.GlAccountSuspendedIncomeId;
        product.GlAccountIncomeInterestId = updated.GlAccountIncomeInterestId;
        product.GlAccountIncomeFeeId = updated.GlAccountIncomeFeeId;
        product.GlAccountIncomePenaltyId = updated.GlAccountIncomePenaltyId;
        product.GlAccountIncomeRecoveryId = updated.GlAccountIncomeRecoveryId;
        product.GlAccountLoansWrittenOffId = updated.GlAccountLoansWrittenOffId;

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanProductWriteResult(LoanProductWriteOutcome.Success, product);
    }

    public async Task<LoanProductWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.NotFound);
        }

        var inUse = await _db.Loans.AnyAsync(l => l.LoanProductId == id, cancellationToken)
            || await _db.LoanApplications.AnyAsync(a => a.LoanProductId == id, cancellationToken);
        if (inUse)
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.InUse, product);
        }

        _db.LoanProducts.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        return new LoanProductWriteResult(LoanProductWriteOutcome.Success);
    }

    public async Task<LoanProductWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.NotFound);
        }

        product.Active = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new LoanProductWriteResult(LoanProductWriteOutcome.Success, product);
    }

    public async Task<LoanProductWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new LoanProductWriteResult(LoanProductWriteOutcome.NotFound);
        }

        product.Active = false;
        await _db.SaveChangesAsync(cancellationToken);
        return new LoanProductWriteResult(LoanProductWriteOutcome.Success, product);
    }

    private static bool IsValidRange(LoanProduct product) =>
        LoanProductValidationRules.IsValidMinDefaultMax(product.MinimumPrincipal, product.DefaultPrincipal, product.MaximumPrincipal)
        && LoanProductValidationRules.IsValidMinDefaultMax(product.MinimumLoanTerm, product.DefaultLoanTerm, product.MaximumLoanTerm)
        && LoanProductValidationRules.IsValidMinDefaultMax(product.MinimumInterestRate, product.DefaultInterestRate, product.MaximumInterestRate);
}
