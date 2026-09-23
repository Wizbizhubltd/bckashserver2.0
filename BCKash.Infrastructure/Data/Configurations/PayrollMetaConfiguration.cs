using BCKash.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PayrollMetaConfiguration : IEntityTypeConfiguration<PayrollMeta>
{
    public void Configure(EntityTypeBuilder<PayrollMeta> builder)
    {
        builder.ToTable("payroll_meta");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(m => m.PayrollId).HasColumnName("payroll_id").IsRequired();
        builder.Property(m => m.PayrollTemplateMetaId).HasColumnName("payroll_template_meta_id");
        builder.Property(m => m.Value).HasColumnName("value").HasPrecision(65, 2);
        builder.Property(m => m.IsTax).HasColumnName("is_tax");
        builder.Property(m => m.IsPercentage).HasColumnName("is_percentage");

        builder.Property(m => m.Position)
            .HasColumnName("position")
            .HasConversion(
                p => p == PayrollMetaPosition.TopLeft ? "top_left"
                    : p == PayrollMetaPosition.TopRight ? "top_right"
                    : p == PayrollMetaPosition.BottomLeft ? "bottom_left"
                    : p == PayrollMetaPosition.BottomRight ? "bottom_right"
                    : null,
                s => s == "top_left" ? PayrollMetaPosition.TopLeft
                    : s == "top_right" ? PayrollMetaPosition.TopRight
                    : s == "bottom_left" ? PayrollMetaPosition.BottomLeft
                    : s == "bottom_right" ? PayrollMetaPosition.BottomRight
                    : (PayrollMetaPosition?)null)
            .HasMaxLength(20);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(m => m.Payroll)
            .WithMany()
            .HasForeignKey(m => m.PayrollId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PayrollTemplateMeta)
            .WithMany()
            .HasForeignKey(m => m.PayrollTemplateMetaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
