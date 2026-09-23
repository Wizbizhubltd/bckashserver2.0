using BCKash.Domain.GeneralLedger;
using BCKash.SharedKernel;

namespace BCKash.Domain.Savings;

/// <summary>Legacy `savings_products.interest_calculation_type`.</summary>
public enum InterestCalculationType
{
    Daily,
    Average
}

/// <summary>Legacy `savings_products.accounting_rule`.</summary>
public enum SavingsAccountingRule
{
    None,
    Cash
}

/// <summary>
/// Maps the legacy `savings_products` table (BRD §6.6). The eight `gl_account_*_id`
/// columns are FKs into `gl_accounts` (inside this table group), so each gets its own
/// navigation — no GL-posting logic is implemented here, this is purely the chart-of-
/// accounts wiring for the product.
/// </summary>
public class SavingsProduct : IHasTimestamps
{
    public int Id { get; set; }

    /// <summary>New in Phase 7 — the legacy schema has no such column, but FR-SAV-1 requires a product to be deactivated (not deleted) once a SavingsAccount exists against it, which needs somewhere to persist "deactivated" (same rationale as LoanProduct.Active, Phase 4).</summary>
    public bool Active { get; set; } = true;

    public int? CreatedById { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int? CurrencyId { get; set; }
    public int Decimals { get; set; } = 2;
    public decimal? InterestRate { get; set; }
    public bool AllowOverdraft { get; set; }
    public decimal? MinimumBalance { get; set; }
    public InterestCompoundingPeriod? InterestCompoundingPeriod { get; set; }
    public InterestPostingPeriod? InterestPostingPeriod { get; set; }
    public InterestCalculationType? InterestCalculationType { get; set; }
    public bool AllowTransferWithdrawalFee { get; set; }
    public decimal? OpeningBalance { get; set; }
    public bool AllowAdditionalCharges { get; set; }
    public SavingsYearDays YearDays { get; set; } = SavingsYearDays.Days365;
    public SavingsAccountingRule AccountingRule { get; set; } = SavingsAccountingRule.Cash;
    public int? GlAccountSavingsReferenceId { get; set; }
    public int? GlAccountOverdraftPortfolioId { get; set; }
    public int? GlAccountSavingsControlId { get; set; }
    public int? GlAccountInterestOnSavingsId { get; set; }
    public int? GlAccountSavingsWrittenOffId { get; set; }
    public int? GlAccountIncomeInterestId { get; set; }
    public int? GlAccountIncomeFeeId { get; set; }
    public int? GlAccountIncomePenaltyId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public GlAccount? GlAccountSavingsReference { get; set; }
    public GlAccount? GlAccountOverdraftPortfolio { get; set; }
    public GlAccount? GlAccountSavingsControl { get; set; }
    public GlAccount? GlAccountInterestOnSavings { get; set; }
    public GlAccount? GlAccountSavingsWrittenOff { get; set; }
    public GlAccount? GlAccountIncomeInterest { get; set; }
    public GlAccount? GlAccountIncomeFee { get; set; }
    public GlAccount? GlAccountIncomePenalty { get; set; }

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = new List<SavingsAccount>();
    public ICollection<SavingsProductCharge> SavingsProductCharges { get; set; } = new List<SavingsProductCharge>();
}
