using BCKash.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.CreatedById).HasColumnName("created_by_id");
        builder.Property(a => a.AssetTypeId).HasColumnName("asset_type_id");
        builder.Property(a => a.OfficeId).HasColumnName("office_id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(a => a.PurchaseDate).HasColumnName("purchase_date").HasColumnType("date");
        builder.Property(a => a.PurchasePrice).HasColumnName("purchase_price").HasPrecision(65, 2);
        builder.Property(a => a.Value).HasColumnName("value").HasPrecision(65, 2);
        builder.Property(a => a.LifeSpan).HasColumnName("life_span");
        builder.Property(a => a.SalvageValue).HasColumnName("salvage_value").HasPrecision(65, 2);
        builder.Property(a => a.SerialNumber).HasColumnName("serial_number");
        builder.Property(a => a.Notes).HasColumnName("notes");
        builder.Property(a => a.Files).HasColumnName("files");
        builder.Property(a => a.PurchaseYear).HasColumnName("purchase_year");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == AssetStatus.Active ? "active"
                    : s == AssetStatus.Inactive ? "inactive"
                    : s == AssetStatus.Sold ? "sold"
                    : s == AssetStatus.Damaged ? "damaged"
                    : s == AssetStatus.WrittenOff ? "written_off"
                    : null,
                s => s == "active" ? AssetStatus.Active
                    : s == "inactive" ? AssetStatus.Inactive
                    : s == "sold" ? AssetStatus.Sold
                    : s == "damaged" ? AssetStatus.Damaged
                    : s == "written_off" ? AssetStatus.WrittenOff
                    : (AssetStatus?)null)
            .HasMaxLength(20);

        builder.Property(a => a.Active).HasColumnName("active");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(a => a.AssetType)
            .WithMany(t => t.Assets)
            .HasForeignKey(a => a.AssetTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
