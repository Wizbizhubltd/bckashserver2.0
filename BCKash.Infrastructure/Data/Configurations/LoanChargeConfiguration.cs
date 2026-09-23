using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanChargeConfiguration : IEntityTypeConfiguration<LoanCharge>
{
    public void Configure(EntityTypeBuilder<LoanCharge> builder)
    {
        builder.ToTable("loan_charges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.LoanId).HasColumnName("loan_id");
        builder.Property(c => c.ChargeId).HasColumnName("charge_id");
        builder.Property(c => c.Penalty).HasColumnName("penalty").IsRequired();
        builder.Property(c => c.Waived).HasColumnName("waived").IsRequired();

        builder.Property(c => c.ChargeType)
            .HasColumnName("charge_type")
            .HasConversion(
                v => v == LoanChargeType.Disbursement ? "disbursement"
                    : v == LoanChargeType.DisbursementRepayment ? "disbursement_repayment"
                    : v == LoanChargeType.SpecifiedDueDate ? "specified_due_date"
                    : v == LoanChargeType.InstallmentFee ? "installment_fee"
                    : v == LoanChargeType.OverdueInstallmentFee ? "overdue_installment_fee"
                    : v == LoanChargeType.LoanReschedulingFee ? "loan_rescheduling_fee"
                    : "overdue_maturity",
                v => v == "disbursement" ? LoanChargeType.Disbursement
                    : v == "disbursement_repayment" ? LoanChargeType.DisbursementRepayment
                    : v == "specified_due_date" ? LoanChargeType.SpecifiedDueDate
                    : v == "installment_fee" ? LoanChargeType.InstallmentFee
                    : v == "overdue_installment_fee" ? LoanChargeType.OverdueInstallmentFee
                    : v == "loan_rescheduling_fee" ? LoanChargeType.LoanReschedulingFee
                    : LoanChargeType.OverdueMaturity)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.ChargeOption)
            .HasColumnName("charge_option")
            .HasConversion(
                v => v == LoanChargeCalculationType.Flat ? "flat"
                    : v == LoanChargeCalculationType.Percentage ? "percentage"
                    : v == LoanChargeCalculationType.InstallmentPrincipalDue ? "installment_principal_due"
                    : v == LoanChargeCalculationType.InstallmentPrincipalInterestDue ? "installment_principal_interest_due"
                    : v == LoanChargeCalculationType.InstallmentInterestDue ? "installment_interest_due"
                    : v == LoanChargeCalculationType.InstallmentTotalDue ? "installment_total_due"
                    : v == LoanChargeCalculationType.TotalDue ? "total_due"
                    : "original_principal",
                v => v == "flat" ? LoanChargeCalculationType.Flat
                    : v == "percentage" ? LoanChargeCalculationType.Percentage
                    : v == "installment_principal_due" ? LoanChargeCalculationType.InstallmentPrincipalDue
                    : v == "installment_principal_interest_due" ? LoanChargeCalculationType.InstallmentPrincipalInterestDue
                    : v == "installment_interest_due" ? LoanChargeCalculationType.InstallmentInterestDue
                    : v == "installment_total_due" ? LoanChargeCalculationType.InstallmentTotalDue
                    : v == "total_due" ? LoanChargeCalculationType.TotalDue
                    : LoanChargeCalculationType.OriginalPrincipal)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(c => c.AmountPaid).HasColumnName("amount_paid").HasPrecision(65, 2);
        builder.Property(c => c.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(c => c.GracePeriod).HasColumnName("grace_period").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.LoanId);

        builder.HasOne(c => c.Loan)
            .WithMany(l => l.LoanCharges)
            .HasForeignKey(c => c.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
