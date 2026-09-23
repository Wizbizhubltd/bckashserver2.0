using BCKash.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class OtherIncomeTypeConfiguration : IEntityTypeConfiguration<OtherIncomeType>
{
    public void Configure(EntityTypeBuilder<OtherIncomeType> builder)
    {
        builder.ToTable("other_income_types");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(t => t.GlAccountAssetId).HasColumnName("gl_account_asset_id");
        builder.Property(t => t.GlAccountIncomeId).HasColumnName("gl_account_income_id");
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
