using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SavingsProductConfiguration : IEntityTypeConfiguration<SavingsProduct>
{
    public void Configure(EntityTypeBuilder<SavingsProduct> builder)
    {
        builder.ToTable("savings_products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.Active).HasColumnName("active").IsRequired();
        builder.Property(p => p.CreatedById).HasColumnName("created_by_id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(p => p.ShortName).HasColumnName("short_name").HasMaxLength(191);
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.CurrencyId).HasColumnName("currency_id");
        builder.Property(p => p.Decimals).HasColumnName("decimals").IsRequired();
        builder.Property(p => p.InterestRate).HasColumnName("interest_rate").HasPrecision(65, 4);
        builder.Property(p => p.AllowOverdraft).HasColumnName("allow_overdraft").IsRequired();
        builder.Property(p => p.MinimumBalance).HasColumnName("minimum_balance").HasPrecision(65, 4);

        builder.Property(p => p.InterestCompoundingPeriod)
            .HasColumnName("interest_compounding_period")
            .HasConversion(
                c => c == null ? null : c == InterestCompoundingPeriod.Daily ? "daily" : c == InterestCompoundingPeriod.Monthly ? "monthly" : c == InterestCompoundingPeriod.Quarterly ? "quarterly" : c == InterestCompoundingPeriod.Biannual ? "biannual" : "annually",
                s => s == null ? (InterestCompoundingPeriod?)null : s == "daily" ? InterestCompoundingPeriod.Daily : s == "monthly" ? InterestCompoundingPeriod.Monthly : s == "quarterly" ? InterestCompoundingPeriod.Quarterly : s == "biannual" ? InterestCompoundingPeriod.Biannual : InterestCompoundingPeriod.Annually)
            .HasMaxLength(20);

        builder.Property(p => p.InterestPostingPeriod)
            .HasColumnName("interest_posting_period")
            .HasConversion(
                p2 => p2 == null ? null : p2 == InterestPostingPeriod.Monthly ? "monthly" : p2 == InterestPostingPeriod.Quarterly ? "quarterly" : p2 == InterestPostingPeriod.Biannual ? "biannual" : "annually",
                s => s == null ? (InterestPostingPeriod?)null : s == "monthly" ? InterestPostingPeriod.Monthly : s == "quarterly" ? InterestPostingPeriod.Quarterly : s == "biannual" ? InterestPostingPeriod.Biannual : InterestPostingPeriod.Annually)
            .HasMaxLength(20);

        builder.Property(p => p.InterestCalculationType)
            .HasColumnName("interest_calculation_type")
            .HasConversion(
                c => c == null ? null : c == InterestCalculationType.Daily ? "daily" : "average",
                s => s == null ? (InterestCalculationType?)null : s == "daily" ? InterestCalculationType.Daily : InterestCalculationType.Average)
            .HasMaxLength(20);

        builder.Property(p => p.AllowTransferWithdrawalFee).HasColumnName("allow_transfer_withdrawal_fee").IsRequired();
        builder.Property(p => p.OpeningBalance).HasColumnName("opening_balance").HasPrecision(65, 4);
        builder.Property(p => p.AllowAdditionalCharges).HasColumnName("allow_additional_charges").IsRequired();

        builder.Property(p => p.YearDays)
            .HasColumnName("year_days")
            .HasConversion(
                y => y == SavingsYearDays.Days360 ? "360" : "365",
                s => s == "360" ? SavingsYearDays.Days360 : SavingsYearDays.Days365)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.AccountingRule)
            .HasColumnName("accounting_rule")
            .HasConversion(
                a => a == SavingsAccountingRule.None ? "none" : "cash",
                s => s == "none" ? SavingsAccountingRule.None : SavingsAccountingRule.Cash)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.GlAccountSavingsReferenceId).HasColumnName("gl_account_savings_reference_id");
        builder.Property(p => p.GlAccountOverdraftPortfolioId).HasColumnName("gl_account_overdraft_portfolio_id");
        builder.Property(p => p.GlAccountSavingsControlId).HasColumnName("gl_account_savings_control_id");
        builder.Property(p => p.GlAccountInterestOnSavingsId).HasColumnName("gl_account_interest_on_savings_id");
        builder.Property(p => p.GlAccountSavingsWrittenOffId).HasColumnName("gl_account_savings_written_off_id");
        builder.Property(p => p.GlAccountIncomeInterestId).HasColumnName("gl_account_income_interest_id");
        builder.Property(p => p.GlAccountIncomeFeeId).HasColumnName("gl_account_income_fee_id");
        builder.Property(p => p.GlAccountIncomePenaltyId).HasColumnName("gl_account_income_penalty_id");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.GlAccountSavingsReference)
            .WithMany()
            .HasForeignKey(p => p.GlAccountSavingsReferenceId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountOverdraftPortfolio)
            .WithMany()
            .HasForeignKey(p => p.GlAccountOverdraftPortfolioId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountSavingsControl)
            .WithMany()
            .HasForeignKey(p => p.GlAccountSavingsControlId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountInterestOnSavings)
            .WithMany()
            .HasForeignKey(p => p.GlAccountInterestOnSavingsId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountSavingsWrittenOff)
            .WithMany()
            .HasForeignKey(p => p.GlAccountSavingsWrittenOffId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountIncomeInterest)
            .WithMany()
            .HasForeignKey(p => p.GlAccountIncomeInterestId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountIncomeFee)
            .WithMany()
            .HasForeignKey(p => p.GlAccountIncomeFeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.GlAccountIncomePenalty)
            .WithMany()
            .HasForeignKey(p => p.GlAccountIncomePenaltyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
