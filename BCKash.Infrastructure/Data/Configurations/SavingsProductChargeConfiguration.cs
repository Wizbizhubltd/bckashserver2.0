using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SavingsProductChargeConfiguration : IEntityTypeConfiguration<SavingsProductCharge>
{
    public void Configure(EntityTypeBuilder<SavingsProductCharge> builder)
    {
        builder.ToTable("savings_product_charges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.ChargeId).HasColumnName("charge_id");
        builder.Property(c => c.SavingsProductId).HasColumnName("savings_product_id");
        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(c => c.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(c => c.GracePeriod).HasColumnName("grace_period").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.SavingsProduct)
            .WithMany(p => p.SavingsProductCharges)
            .HasForeignKey(c => c.SavingsProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
