using BCKash.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
{
    public void Configure(EntityTypeBuilder<AssetType> builder)
    {
        builder.ToTable("asset_types");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(t => t.GlAccountFixedAssetId).HasColumnName("gl_account_fixed_asset_id");
        builder.Property(t => t.GlAccountAssetId).HasColumnName("gl_account_asset_id");
        builder.Property(t => t.GlAccountContraAssetId).HasColumnName("gl_account_contra_asset_id");
        builder.Property(t => t.GlAccountExpenseId).HasColumnName("gl_account_expense_id");
        builder.Property(t => t.GlAccountLiabilityId).HasColumnName("gl_account_liability_id");
        builder.Property(t => t.GlAccountIncomeId).HasColumnName("gl_account_income_id");
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
