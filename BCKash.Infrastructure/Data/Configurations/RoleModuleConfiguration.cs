using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class RoleModuleConfiguration : IEntityTypeConfiguration<RoleModule>
{
    public void Configure(EntityTypeBuilder<RoleModule> builder)
    {
        builder.ToTable("role_modules");
        builder.HasKey(rm => new { rm.RoleId, rm.Module });

        builder.Property(rm => rm.RoleId).HasColumnName("role_id");
        builder.Property(rm => rm.Module).HasColumnName("module").HasMaxLength(50);

        builder.HasOne(rm => rm.Role)
            .WithMany()
            .HasForeignKey(rm => rm.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
