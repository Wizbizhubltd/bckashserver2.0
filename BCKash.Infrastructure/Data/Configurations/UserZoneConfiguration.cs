using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class UserZoneConfiguration : IEntityTypeConfiguration<UserZone>
{
    public void Configure(EntityTypeBuilder<UserZone> builder)
    {
        builder.ToTable("user_zones");
        builder.HasKey(uz => new { uz.UserId, uz.ZoneId });

        builder.Property(uz => uz.UserId).HasColumnName("user_id");
        builder.Property(uz => uz.ZoneId).HasColumnName("zone_id");
        builder.Property(uz => uz.AssignedById).HasColumnName("assigned_by_id");
        builder.Property(uz => uz.CreatedAt).HasColumnName("created_at");

        builder.HasOne(uz => uz.User)
            .WithMany(u => u.UserZones)
            .HasForeignKey(uz => uz.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uz => uz.Zone)
            .WithMany()
            .HasForeignKey(uz => uz.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
