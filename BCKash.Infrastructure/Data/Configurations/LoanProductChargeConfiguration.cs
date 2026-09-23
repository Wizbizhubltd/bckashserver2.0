using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanProductChargeConfiguration : IEntityTypeConfiguration<LoanProductCharge>
{
    public void Configure(EntityTypeBuilder<LoanProductCharge> builder)
    {
        builder.ToTable("loan_product_charges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.LoanProductId).HasColumnName("loan_product_id");
        builder.Property(c => c.ChargeId).HasColumnName("charge_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.LoanProduct)
            .WithMany(p => p.LoanProductCharges)
            .HasForeignKey(c => c.LoanProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
