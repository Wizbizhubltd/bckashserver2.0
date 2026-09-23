using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("charges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(c => c.CurrencyId).HasColumnName("currency_id");

        builder.Property(c => c.Product)
            .HasColumnName("product")
            .HasConversion(
                p => p == ChargeProduct.Loan ? "loan"
                    : p == ChargeProduct.Savings ? "savings"
                    : p == ChargeProduct.Shares ? "shares"
                    : "client",
                s => s == "loan" ? ChargeProduct.Loan
                    : s == "savings" ? ChargeProduct.Savings
                    : s == "shares" ? ChargeProduct.Shares
                    : ChargeProduct.Client)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ChargeType)
            .HasColumnName("charge_type")
            .HasConversion(
                t => t == ChargeType.Disbursement ? "disbursement"
                    : t == ChargeType.DisbursementRepayment ? "disbursement_repayment"
                    : t == ChargeType.SpecifiedDueDate ? "specified_due_date"
                    : t == ChargeType.InstallmentFee ? "installment_fee"
                    : t == ChargeType.OverdueInstallmentFee ? "overdue_installment_fee"
                    : t == ChargeType.LoanReschedulingFee ? "loan_rescheduling_fee"
                    : t == ChargeType.OverdueMaturity ? "overdue_maturity"
                    : t == ChargeType.SavingsActivation ? "savings_activation"
                    : t == ChargeType.WithdrawalFee ? "withdrawal_fee"
                    : t == ChargeType.AnnualFee ? "annual_fee"
                    : t == ChargeType.MonthlyFee ? "monthly_fee"
                    : t == ChargeType.Activation ? "activation"
                    : t == ChargeType.SharesPurchase ? "shares_purchase"
                    : "shares_redeem",
                s => s == "disbursement" ? ChargeType.Disbursement
                    : s == "disbursement_repayment" ? ChargeType.DisbursementRepayment
                    : s == "specified_due_date" ? ChargeType.SpecifiedDueDate
                    : s == "installment_fee" ? ChargeType.InstallmentFee
                    : s == "overdue_installment_fee" ? ChargeType.OverdueInstallmentFee
                    : s == "loan_rescheduling_fee" ? ChargeType.LoanReschedulingFee
                    : s == "overdue_maturity" ? ChargeType.OverdueMaturity
                    : s == "savings_activation" ? ChargeType.SavingsActivation
                    : s == "withdrawal_fee" ? ChargeType.WithdrawalFee
                    : s == "annual_fee" ? ChargeType.AnnualFee
                    : s == "monthly_fee" ? ChargeType.MonthlyFee
                    : s == "activation" ? ChargeType.Activation
                    : s == "shares_purchase" ? ChargeType.SharesPurchase
                    : ChargeType.SharesRedeem)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.ChargeOption)
            .HasColumnName("charge_option")
            .HasConversion(
                o => o == ChargeOption.Flat ? "flat"
                    : o == ChargeOption.Percentage ? "percentage"
                    : o == ChargeOption.InstallmentPrincipalDue ? "installment_principal_due"
                    : o == ChargeOption.InstallmentPrincipalInterestDue ? "installment_principal_interest_due"
                    : o == ChargeOption.InstallmentInterestDue ? "installment_interest_due"
                    : o == ChargeOption.InstallmentTotalDue ? "installment_total_due"
                    : o == ChargeOption.TotalDue ? "total_due"
                    : o == ChargeOption.PrincipalDue ? "principal_due"
                    : o == ChargeOption.InterestDue ? "interest_due"
                    : o == ChargeOption.TotalOutstanding ? "total_outstanding"
                    : "original_principal",
                s => s == "flat" ? ChargeOption.Flat
                    : s == "percentage" ? ChargeOption.Percentage
                    : s == "installment_principal_due" ? ChargeOption.InstallmentPrincipalDue
                    : s == "installment_principal_interest_due" ? ChargeOption.InstallmentPrincipalInterestDue
                    : s == "installment_interest_due" ? ChargeOption.InstallmentInterestDue
                    : s == "installment_total_due" ? ChargeOption.InstallmentTotalDue
                    : s == "total_due" ? ChargeOption.TotalDue
                    : s == "principal_due" ? ChargeOption.PrincipalDue
                    : s == "interest_due" ? ChargeOption.InterestDue
                    : s == "total_outstanding" ? ChargeOption.TotalOutstanding
                    : ChargeOption.OriginalPrincipal)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.ChargeFrequency).HasColumnName("charge_frequency").IsRequired();

        builder.Property(c => c.ChargeFrequencyType)
            .HasColumnName("charge_frequency_type")
            .HasConversion(
                t => t == ChargeFrequencyType.Days ? "days"
                    : t == ChargeFrequencyType.Weeks ? "weeks"
                    : t == ChargeFrequencyType.Months ? "months"
                    : "years",
                s => s == "days" ? ChargeFrequencyType.Days
                    : s == "weeks" ? ChargeFrequencyType.Weeks
                    : s == "months" ? ChargeFrequencyType.Months
                    : ChargeFrequencyType.Years)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ChargeFrequencyAmount).HasColumnName("charge_frequency_amount").IsRequired();
        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(c => c.MinimumAmount).HasColumnName("minimum_amount").HasPrecision(65, 2);
        builder.Property(c => c.MaximumAmount).HasColumnName("maximum_amount").HasPrecision(65, 2);

        builder.Property(c => c.ChargePaymentMode)
            .HasColumnName("charge_payment_mode")
            .HasConversion(
                m => m == ChargePaymentMode.Regular ? "regular" : "account_transfer",
                s => s == "regular" ? ChargePaymentMode.Regular : ChargePaymentMode.AccountTransfer)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Active).HasColumnName("active");
        builder.Property(c => c.Penalty).HasColumnName("penalty");
        builder.Property(c => c.Override).HasColumnName("override");
        builder.Property(c => c.GlAccountIncomeId).HasColumnName("gl_account_income_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}
