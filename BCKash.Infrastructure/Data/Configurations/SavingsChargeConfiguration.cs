using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SavingsChargeConfiguration : IEntityTypeConfiguration<SavingsCharge>
{
    public void Configure(EntityTypeBuilder<SavingsCharge> builder)
    {
        builder.ToTable("savings_charges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.SavingsId).HasColumnName("savings_id");
        builder.Property(c => c.ChargeId).HasColumnName("charge_id");
        builder.Property(c => c.Penalty).HasColumnName("penalty").IsRequired();
        builder.Property(c => c.Waived).HasColumnName("waived").IsRequired();

        builder.Property(c => c.ChargeType)
            .HasColumnName("charge_type")
            .HasConversion(
                t => t == SavingsChargeType.SavingsActivation ? "savings_activation"
                    : t == SavingsChargeType.WithdrawalFee ? "withdrawal_fee"
                    : t == SavingsChargeType.AnnualFee ? "annual_fee"
                    : t == SavingsChargeType.MonthlyFee ? "monthly_fee"
                    : "specified_due_date",
                s => s == "savings_activation" ? SavingsChargeType.SavingsActivation
                    : s == "withdrawal_fee" ? SavingsChargeType.WithdrawalFee
                    : s == "annual_fee" ? SavingsChargeType.AnnualFee
                    : s == "monthly_fee" ? SavingsChargeType.MonthlyFee
                    : SavingsChargeType.SpecifiedDueDate)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.ChargeOption)
            .HasColumnName("charge_option")
            .HasConversion(
                o => o == SavingsChargeOption.Flat ? "flat" : "percentage",
                s => s == "flat" ? SavingsChargeOption.Flat : SavingsChargeOption.Percentage)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(c => c.AmountPaid).HasColumnName("amount_paid").HasPrecision(65, 2);
        builder.Property(c => c.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(c => c.GracePeriod).HasColumnName("grace_period").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.Savings)
            .WithMany()
            .HasForeignKey(c => c.SavingsId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
