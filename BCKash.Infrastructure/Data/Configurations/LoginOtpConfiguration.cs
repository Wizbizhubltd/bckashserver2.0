using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoginOtpConfiguration : IEntityTypeConfiguration<LoginOtp>
{
    public void Configure(EntityTypeBuilder<LoginOtp> builder)
    {
        builder.ToTable("login_otps");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(o => o.Attempts).HasColumnName("attempts");
        builder.Property(o => o.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(o => o.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(o => o.UserId).HasDatabaseName("login_otps_user_id_index");

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
