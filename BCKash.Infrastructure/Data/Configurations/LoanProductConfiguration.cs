using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.ToTable("loan_products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.CreatedById).HasColumnName("created_by_id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(p => p.ShortName).HasColumnName("short_name").HasMaxLength(191);
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.FundId).HasColumnName("fund_id");
        builder.Property(p => p.CurrencyId).HasColumnName("currency_id");
        builder.Property(p => p.Decimals).HasColumnName("decimals").IsRequired();

        builder.Property(p => p.MinimumPrincipal).HasColumnName("minimum_principal").HasPrecision(65, 4);
        builder.Property(p => p.DefaultPrincipal).HasColumnName("default_principal").HasPrecision(65, 4);
        builder.Property(p => p.MaximumPrincipal).HasColumnName("maximum_principal").HasPrecision(65, 4);

        builder.Property(p => p.MinimumLoanTerm).HasColumnName("minimum_loan_term");
        builder.Property(p => p.DefaultLoanTerm).HasColumnName("default_loan_term");
        builder.Property(p => p.MaximumLoanTerm).HasColumnName("maximum_loan_term");

        builder.Property(p => p.RepaymentFrequency).HasColumnName("repayment_frequency");
        builder.Property(p => p.RepaymentFrequencyType)
            .HasColumnName("repayment_frequency_type")
            .HasConversion(
                v => v == FrequencyType.Days ? "days" : v == FrequencyType.Weeks ? "weeks" : v == FrequencyType.Months ? "months" : "years",
                v => v == "days" ? FrequencyType.Days : v == "weeks" ? FrequencyType.Weeks : v == "months" ? FrequencyType.Months : FrequencyType.Years)
            .HasMaxLength(20);

        builder.Property(p => p.MinimumInterestRate).HasColumnName("minimum_interest_rate").HasPrecision(65, 4);
        builder.Property(p => p.DefaultInterestRate).HasColumnName("default_interest_rate").HasPrecision(65, 4);
        builder.Property(p => p.MaximumInterestRate).HasColumnName("maximum_interest_rate").HasPrecision(65, 4);

        builder.Property(p => p.InterestRateType)
            .HasColumnName("interest_rate_type")
            .HasConversion(
                v => v == InterestRateFrequencyType.Day ? "day" : v == InterestRateFrequencyType.Week ? "week" : v == InterestRateFrequencyType.Month ? "month" : "year",
                v => v == "day" ? InterestRateFrequencyType.Day : v == "week" ? InterestRateFrequencyType.Week : v == "month" ? InterestRateFrequencyType.Month : InterestRateFrequencyType.Year)
            .HasMaxLength(20);

        builder.Property(p => p.GraceOnInterestCharged).HasColumnName("grace_on_interest_charged");
        builder.Property(p => p.GraceOnPrincipal).HasColumnName("grace_on_principal");
        builder.Property(p => p.GraceOnInterestPayment).HasColumnName("grace_on_interest_payment");
        builder.Property(p => p.AllowCustomGrace).HasColumnName("allow_custom_grace").IsRequired();
        builder.Property(p => p.AllowStandingInstructions).HasColumnName("allow_standing_instuctions").IsRequired();

        builder.Property(p => p.InterestMethod)
            .HasColumnName("interest_method")
            .HasConversion(
                v => v == LoanInterestMethod.Flat ? "flat" : "declining_balance",
                v => v == "flat" ? LoanInterestMethod.Flat : LoanInterestMethod.DecliningBalance)
            .HasMaxLength(30);

        builder.Property(p => p.AmortizationMethod)
            .HasColumnName("armotization_method")
            .HasConversion(
                v => v == LoanAmortizationMethod.EqualInstallment ? "equal_installment" : "equal_principal",
                v => v == "equal_installment" ? LoanAmortizationMethod.EqualInstallment : LoanAmortizationMethod.EqualPrincipal)
            .HasMaxLength(30);

        builder.Property(p => p.InterestCalculationPeriodType)
            .HasColumnName("interest_calculation_period_type")
            .HasConversion(
                v => v == InterestCalculationPeriodType.Daily ? "daily" : "same",
                v => v == "daily" ? InterestCalculationPeriodType.Daily : InterestCalculationPeriodType.Same)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.YearDays)
            .HasColumnName("year_days")
            .HasConversion(
                v => v == YearDaysType.Actual ? "actual" : v == YearDaysType.Days360 ? "360" : v == YearDaysType.Days364 ? "364" : "365",
                v => v == "actual" ? YearDaysType.Actual : v == "360" ? YearDaysType.Days360 : v == "364" ? YearDaysType.Days364 : YearDaysType.Days365)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.MonthDays)
            .HasColumnName("month_days")
            .HasConversion(
                v => v == MonthDaysType.Actual ? "actual" : v == MonthDaysType.Days30 ? "30" : "31",
                v => v == "actual" ? MonthDaysType.Actual : v == "30" ? MonthDaysType.Days30 : MonthDaysType.Days31)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.LoanTransactionStrategy)
            .HasColumnName("loan_transaction_strategy")
            .HasConversion(
                v => v == LoanTransactionStrategy.PenaltyFeesInterestPrincipal ? "penalty_fees_interest_principal"
                    : v == LoanTransactionStrategy.PrincipalInterestPenaltyFees ? "principal_interest_penalty_fees"
                    : "interest_principal_penalty_fees",
                v => v == "penalty_fees_interest_principal" ? LoanTransactionStrategy.PenaltyFeesInterestPrincipal
                    : v == "principal_interest_penalty_fees" ? LoanTransactionStrategy.PrincipalInterestPenaltyFees
                    : LoanTransactionStrategy.InterestPrincipalPenaltyFees)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(p => p.IncludeInCycle).HasColumnName("include_in_cycle").IsRequired();
        builder.Property(p => p.LockGuarantee).HasColumnName("lock_guarantee").IsRequired();
        builder.Property(p => p.AllocateOverpayments).HasColumnName("allocate_overpayments").IsRequired();
        builder.Property(p => p.AllowAdditionalCharges).HasColumnName("allow_additional_charges").IsRequired();

        builder.Property(p => p.AccountingRule)
            .HasColumnName("accounting_rule")
            .HasConversion(
                v => v == LoanAccountingRule.None ? "none" : v == LoanAccountingRule.Cash ? "cash" : v == LoanAccountingRule.AccrualPeriodic ? "accrual_periodic" : "accrual_upfront",
                v => v == "none" ? LoanAccountingRule.None : v == "cash" ? LoanAccountingRule.Cash : v == "accrual_periodic" ? LoanAccountingRule.AccrualPeriodic : LoanAccountingRule.AccrualUpfront)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.NpaDays).HasColumnName("npa_days");
        builder.Property(p => p.ArrearsGraceDays).HasColumnName("arrears_grace_days");
        builder.Property(p => p.NpaSuspendIncome).HasColumnName("npa_suspend_income").IsRequired();

        builder.Property(p => p.GlAccountFundSourceId).HasColumnName("gl_account_fund_source_id");
        builder.Property(p => p.GlAccountLoanPortfolioId).HasColumnName("gl_account_loan_portfolio_id");
        builder.Property(p => p.GlAccountReceivableInterestId).HasColumnName("gl_account_receivable_interest_id");
        builder.Property(p => p.GlAccountReceivableFeeId).HasColumnName("gl_account_receivable_fee_id");
        builder.Property(p => p.GlAccountReceivablePenaltyId).HasColumnName("gl_account_receivable_penalty_id");
        builder.Property(p => p.GlAccountLoanOverPaymentsId).HasColumnName("gl_account_loan_over_payments_id");
        builder.Property(p => p.GlAccountSuspendedIncomeId).HasColumnName("gl_account_suspended_income_id");
        builder.Property(p => p.GlAccountIncomeInterestId).HasColumnName("gl_account_income_interest_id");
        builder.Property(p => p.GlAccountIncomeFeeId).HasColumnName("gl_account_income_fee_id");
        builder.Property(p => p.GlAccountIncomePenaltyId).HasColumnName("gl_account_income_penalty_id");
        builder.Property(p => p.GlAccountIncomeRecoveryId).HasColumnName("gl_account_income_recovery_id");
        builder.Property(p => p.GlAccountLoansWrittenOffId).HasColumnName("gl_account_loans_written_off_id");

        // New in Phase 4 — the legacy schema has no such column; see LoanProduct.cs.
        builder.Property(p => p.Active).HasColumnName("active").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.Name);
    }
}
