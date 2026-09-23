using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_products` table (BRD §6.5).</summary>
public class LoanProduct : IHasTimestamps
{
    public int Id { get; set; }

    /// <summary>New in Phase 4 — the legacy schema has no such column, but FR-LN-1 requires a product to be deactivated (not deleted) once a loan exists against it, which needs somewhere to persist "deactivated".</summary>
    public bool Active { get; set; } = true;

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CreatedById { get; set; }

    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }

    /// <summary>References `funds.id` — outside this table group, kept as a plain scalar.</summary>
    public int? FundId { get; set; }

    /// <summary>References `currencies.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CurrencyId { get; set; }

    public int Decimals { get; set; } = 2;

    public decimal? MinimumPrincipal { get; set; }
    public decimal? DefaultPrincipal { get; set; }
    public decimal? MaximumPrincipal { get; set; }

    public int? MinimumLoanTerm { get; set; }
    public int? DefaultLoanTerm { get; set; }
    public int? MaximumLoanTerm { get; set; }

    public int? RepaymentFrequency { get; set; }
    public FrequencyType? RepaymentFrequencyType { get; set; }

    public decimal? MinimumInterestRate { get; set; }
    public decimal? DefaultInterestRate { get; set; }
    public decimal? MaximumInterestRate { get; set; }
    public InterestRateFrequencyType? InterestRateType { get; set; }

    public int? GraceOnInterestCharged { get; set; }
    public int? GraceOnPrincipal { get; set; }
    public int? GraceOnInterestPayment { get; set; }
    public bool AllowCustomGrace { get; set; }

    /// <summary>Legacy column name is misspelled (`allow_standing_instuctions`); property name is corrected.</summary>
    public bool AllowStandingInstructions { get; set; }

    public LoanInterestMethod? InterestMethod { get; set; }
    public LoanAmortizationMethod? AmortizationMethod { get; set; }

    public InterestCalculationPeriodType InterestCalculationPeriodType { get; set; } = InterestCalculationPeriodType.Same;
    public YearDaysType YearDays { get; set; } = YearDaysType.Days365;
    public MonthDaysType MonthDays { get; set; } = MonthDaysType.Days30;
    public LoanTransactionStrategy LoanTransactionStrategy { get; set; } = LoanTransactionStrategy.InterestPrincipalPenaltyFees;

    public bool IncludeInCycle { get; set; }
    public bool LockGuarantee { get; set; }
    public bool AllocateOverpayments { get; set; }
    public bool AllowAdditionalCharges { get; set; }

    public LoanAccountingRule AccountingRule { get; set; } = LoanAccountingRule.Cash;

    public int? NpaDays { get; set; }
    public int? ArrearsGraceDays { get; set; }
    public bool NpaSuspendIncome { get; set; }

    /// <summary>All gl_account_* columns reference `gl_accounts.id` — outside this table group, kept as plain scalars.</summary>
    public int? GlAccountFundSourceId { get; set; }
    public int? GlAccountLoanPortfolioId { get; set; }
    public int? GlAccountReceivableInterestId { get; set; }
    public int? GlAccountReceivableFeeId { get; set; }
    public int? GlAccountReceivablePenaltyId { get; set; }
    public int? GlAccountLoanOverPaymentsId { get; set; }
    public int? GlAccountSuspendedIncomeId { get; set; }
    public int? GlAccountIncomeInterestId { get; set; }
    public int? GlAccountIncomeFeeId { get; set; }
    public int? GlAccountIncomePenaltyId { get; set; }
    public int? GlAccountIncomeRecoveryId { get; set; }
    public int? GlAccountLoansWrittenOffId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<LoanProductCharge> LoanProductCharges { get; set; } = new List<LoanProductCharge>();
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
