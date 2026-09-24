using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");
        builder.HasKey(z => z.Id);

        builder.Property(z => z.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(z => z.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(z => z.Description).HasColumnName("description");
        builder.Property(z => z.CreatedById).HasColumnName("created_by_id");
        builder.Property(z => z.CreatedAt).HasColumnName("created_at");
        builder.Property(z => z.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(z => z.Name).IsUnique().HasDatabaseName("zones_name_unique");
    }
}
