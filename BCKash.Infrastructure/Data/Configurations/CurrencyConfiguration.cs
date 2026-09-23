using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(191);
        builder.Property(c => c.Symbol).HasColumnName("symbol").HasMaxLength(191);
        builder.Property(c => c.Decimals).HasColumnName("decimals").HasMaxLength(191);
        builder.Property(c => c.Xrate).HasColumnName("xrate").HasPrecision(65, 8);
        builder.Property(c => c.InternationalCode).HasColumnName("international_code").HasMaxLength(191);
        builder.Property(c => c.Active).HasColumnName("active");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}
