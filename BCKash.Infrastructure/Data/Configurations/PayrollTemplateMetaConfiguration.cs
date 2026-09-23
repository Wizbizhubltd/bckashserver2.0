using BCKash.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PayrollTemplateMetaConfiguration : IEntityTypeConfiguration<PayrollTemplateMeta>
{
    public void Configure(EntityTypeBuilder<PayrollTemplateMeta> builder)
    {
        builder.ToTable("payroll_template_meta");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(m => m.PayrollTemplateId).HasColumnName("payroll_template_id").IsRequired();
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(191);

        builder.Property(m => m.Position)
            .HasColumnName("position")
            .HasConversion(
                p => p == PayrollTemplateMetaPosition.TopLeft ? "top_left"
                    : p == PayrollTemplateMetaPosition.TopRight ? "top_right"
                    : p == PayrollTemplateMetaPosition.BottomLeft ? "bottom_left"
                    : p == PayrollTemplateMetaPosition.BottomRight ? "bottom_right"
                    : p == PayrollTemplateMetaPosition.None ? "none"
                    : null,
                s => s == "top_left" ? PayrollTemplateMetaPosition.TopLeft
                    : s == "top_right" ? PayrollTemplateMetaPosition.TopRight
                    : s == "bottom_left" ? PayrollTemplateMetaPosition.BottomLeft
                    : s == "bottom_right" ? PayrollTemplateMetaPosition.BottomRight
                    : s == "none" ? PayrollTemplateMetaPosition.None
                    : (PayrollTemplateMetaPosition?)null)
            .HasMaxLength(20);

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasConversion(
                t => t == PayrollTemplateMetaType.Addition ? "addition"
                    : t == PayrollTemplateMetaType.Deduction ? "deduction"
                    : null,
                s => s == "addition" ? PayrollTemplateMetaType.Addition
                    : s == "deduction" ? PayrollTemplateMetaType.Deduction
                    : (PayrollTemplateMetaType?)null)
            .HasMaxLength(20);

        builder.Property(m => m.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(m => m.IsTax).HasColumnName("is_tax").IsRequired();
        builder.Property(m => m.IsPercentage).HasColumnName("is_percentage").IsRequired();

        builder.Property(m => m.TaxOn)
            .HasColumnName("tax_on")
            .HasConversion(
                t => t == PayrollTemplateMetaTaxOn.Net ? "net"
                    : t == PayrollTemplateMetaTaxOn.Gross ? "gross"
                    : null,
                s => s == "net" ? PayrollTemplateMetaTaxOn.Net
                    : s == "gross" ? PayrollTemplateMetaTaxOn.Gross
                    : (PayrollTemplateMetaTaxOn?)null)
            .HasMaxLength(10);

        builder.Property(m => m.DefaultValue).HasColumnName("default_value").HasPrecision(65, 2);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(m => m.PayrollTemplate)
            .WithMany(t => t.PayrollTemplateMeta)
            .HasForeignKey(m => m.PayrollTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
