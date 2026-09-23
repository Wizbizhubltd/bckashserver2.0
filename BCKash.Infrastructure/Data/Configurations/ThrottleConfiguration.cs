using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ThrottleConfiguration : IEntityTypeConfiguration<Throttle>
{
    public void Configure(EntityTypeBuilder<Throttle> builder)
    {
        builder.ToTable("throttle");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.Type).HasColumnName("type").HasMaxLength(191).IsRequired();
        builder.Property(t => t.Ip).HasColumnName("ip").HasMaxLength(191);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.UserId).HasDatabaseName("throttle_user_id_index");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}
