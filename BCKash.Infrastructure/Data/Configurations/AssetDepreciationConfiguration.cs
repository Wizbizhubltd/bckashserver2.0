using BCKash.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class AssetDepreciationConfiguration : IEntityTypeConfiguration<AssetDepreciation>
{
    public void Configure(EntityTypeBuilder<AssetDepreciation> builder)
    {
        builder.ToTable("asset_depreciation");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(d => d.AssetId).HasColumnName("asset_id");
        builder.Property(d => d.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(d => d.BeginningValue).HasColumnName("beginning_value").HasPrecision(65, 2);
        builder.Property(d => d.DepreciationValue).HasColumnName("depreciation_value").HasPrecision(65, 2);
        builder.Property(d => d.Rate).HasColumnName("rate").HasPrecision(65, 2);
        builder.Property(d => d.Cost).HasColumnName("cost").HasPrecision(65, 2);
        builder.Property(d => d.Accumulated).HasColumnName("accumulated").HasPrecision(65, 2);
        builder.Property(d => d.EndingValue).HasColumnName("ending_value").HasPrecision(65, 2);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(d => d.Asset)
            .WithMany(a => a.AssetDepreciations)
            .HasForeignKey(d => d.AssetId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
