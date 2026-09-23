using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Savings;

public class SavingsProductService : ISavingsProductService
{
    private readonly BCKashDbContext _db;

    public SavingsProductService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<SavingsProductWriteResult> CreateAsync(SavingsProduct product, CancellationToken cancellationToken = default)
    {
        _db.SavingsProducts.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsProductWriteResult(SavingsProductWriteOutcome.Success, product);
    }

    public async Task<SavingsProductWriteResult> UpdateAsync(int id, SavingsProduct updated, CancellationToken cancellationToken = default)
    {
        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new SavingsProductWriteResult(SavingsProductWriteOutcome.NotFound);
        }

        product.Name = updated.Name;
        product.ShortName = updated.ShortName;
        product.Description = updated.Description;
        product.CurrencyId = updated.CurrencyId;
        product.Decimals = updated.Decimals;
        product.InterestRate = updated.InterestRate;
        product.AllowOverdraft = updated.AllowOverdraft;
        product.MinimumBalance = updated.MinimumBalance;
        product.InterestCompoundingPeriod = updated.InterestCompoundingPeriod;
        product.InterestPostingPeriod = updated.InterestPostingPeriod;
        product.InterestCalculationType = updated.InterestCalculationType;
        product.AllowTransferWithdrawalFee = updated.AllowTransferWithdrawalFee;
        product.OpeningBalance = updated.OpeningBalance;
        product.AllowAdditionalCharges = updated.AllowAdditionalCharges;
        product.YearDays = updated.YearDays;
        product.AccountingRule = updated.AccountingRule;
        product.GlAccountSavingsReferenceId = updated.GlAccountSavingsReferenceId;
        product.GlAccountOverdraftPortfolioId = updated.GlAccountOverdraftPortfolioId;
        product.GlAccountSavingsControlId = updated.GlAccountSavingsControlId;
        product.GlAccountInterestOnSavingsId = updated.GlAccountInterestOnSavingsId;
        product.GlAccountSavingsWrittenOffId = updated.GlAccountSavingsWrittenOffId;
        product.GlAccountIncomeInterestId = updated.GlAccountIncomeInterestId;
        product.GlAccountIncomeFeeId = updated.GlAccountIncomeFeeId;
        product.GlAccountIncomePenaltyId = updated.GlAccountIncomePenaltyId;

        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsProductWriteResult(SavingsProductWriteOutcome.Success, product);
    }

    public async Task<SavingsProductWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new SavingsProductWriteResult(SavingsProductWriteOutcome.NotFound);
        }

        if (await _db.Savings.AnyAsync(s => s.SavingsProductId == id, cancellationToken))
        {
            return new SavingsProductWriteResult(SavingsProductWriteOutcome.InUse, product);
        }

        _db.SavingsProducts.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsProductWriteResult(SavingsProductWriteOutcome.Success);
    }

    public async Task<SavingsProductWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new SavingsProductWriteResult(SavingsProductWriteOutcome.NotFound);
        }

        product.Active = true;
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsProductWriteResult(SavingsProductWriteOutcome.Success, product);
    }

    public async Task<SavingsProductWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.SavingsProducts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return new SavingsProductWriteResult(SavingsProductWriteOutcome.NotFound);
        }

        product.Active = false;
        await _db.SaveChangesAsync(cancellationToken);
        return new SavingsProductWriteResult(SavingsProductWriteOutcome.Success, product);
    }
}
