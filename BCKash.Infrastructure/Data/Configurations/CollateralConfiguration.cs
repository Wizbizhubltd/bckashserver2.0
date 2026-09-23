using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CollateralConfiguration : IEntityTypeConfiguration<Collateral>
{
    public void Configure(EntityTypeBuilder<Collateral> builder)
    {
        builder.ToTable("collateral");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.LoanId).HasColumnName("loan_id");

        // New in Phase 4 — the legacy schema has no such column; see Collateral.cs.
        builder.Property(c => c.LoanApplicationId).HasColumnName("loan_application_id");

        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.CollateralTypeId).HasColumnName("collateral_type_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(c => c.Serial).HasColumnName("serial").HasMaxLength(191);
        builder.Property(c => c.Value).HasColumnName("value").HasPrecision(65, 4);
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.Picture).HasColumnName("picture");
        builder.Property(c => c.Gallery).HasColumnName("gallery");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.LoanId);
        builder.HasIndex(c => c.LoanApplicationId);

        builder.HasOne(c => c.Loan)
            .WithMany(l => l.Collaterals)
            .HasForeignKey(c => c.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(c => c.LoanApplication)
            .WithMany(a => a.Collaterals)
            .HasForeignKey(c => c.LoanApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(c => c.CollateralType)
            .WithMany(t => t.Collaterals)
            .HasForeignKey(c => c.CollateralTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
