using BCKash.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PayrollTemplateConfiguration : IEntityTypeConfiguration<PayrollTemplate>
{
    public void Configure(EntityTypeBuilder<PayrollTemplate> builder)
    {
        builder.ToTable("payroll_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.Picture).HasColumnName("picture").HasMaxLength(191);
        builder.Property(t => t.Active).HasColumnName("active");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
