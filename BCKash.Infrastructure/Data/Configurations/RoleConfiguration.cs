using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.Slug).HasColumnName("slug").HasMaxLength(191).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(191).IsRequired();
        builder.Property(r => r.TimeLimit).HasColumnName("time_limit");
        builder.Property(r => r.FromTime).HasColumnName("from_time").HasMaxLength(191);
        builder.Property(r => r.ToTime).HasColumnName("to_time").HasMaxLength(191);
        builder.Property(r => r.AccessDays).HasColumnName("access_days");
        builder.Property(r => r.LegacyPermissionsRaw).HasColumnName("permissions");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}
