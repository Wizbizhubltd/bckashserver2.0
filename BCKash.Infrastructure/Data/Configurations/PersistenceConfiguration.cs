using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PersistenceConfiguration : IEntityTypeConfiguration<Persistence>
{
    public void Configure(EntityTypeBuilder<Persistence> builder)
    {
        builder.ToTable("persistences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(191).IsRequired();
        builder.Property(p => p.SessionId).HasColumnName("session_id").HasMaxLength(64);
        builder.Property(p => p.DeviceId).HasColumnName("device_id").HasMaxLength(128);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("persistences_code_unique");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
