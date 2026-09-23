using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class RoleUserConfiguration : IEntityTypeConfiguration<RoleUser>
{
    public void Configure(EntityTypeBuilder<RoleUser> builder)
    {
        builder.ToTable("role_users");
        builder.HasKey(ru => new { ru.UserId, ru.RoleId });

        builder.Property(ru => ru.UserId).HasColumnName("user_id");
        builder.Property(ru => ru.RoleId).HasColumnName("role_id");
        builder.Property(ru => ru.CreatedAt).HasColumnName("created_at");
        builder.Property(ru => ru.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(ru => ru.User)
            .WithMany(u => u.RoleUsers)
            .HasForeignKey(ru => ru.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ru => ru.Role)
            .WithMany(r => r.RoleUsers)
            .HasForeignKey(ru => ru.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
