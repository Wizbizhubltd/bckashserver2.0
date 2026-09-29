using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientBiometricConfiguration : IEntityTypeConfiguration<ClientBiometric>
{
    public void Configure(EntityTypeBuilder<ClientBiometric> builder)
    {
        builder.ToTable("client_biometrics");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(b => b.ClientId).HasColumnName("client_id");
        builder.Property(b => b.Purpose).HasColumnName("purpose").HasMaxLength(20).IsRequired();
        builder.Property(b => b.LoanId).HasColumnName("loan_id");
        builder.Property(b => b.SessionId).HasColumnName("session_id").HasMaxLength(100).IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(b => b.LivenessConfidence).HasColumnName("liveness_confidence").HasPrecision(5, 2);
        builder.Property(b => b.Similarity).HasColumnName("similarity").HasPrecision(5, 2);
        builder.Property(b => b.ImageLocation).HasColumnName("image_location").HasMaxLength(500);
        builder.Property(b => b.FailureReason).HasColumnName("failure_reason");
        builder.Property(b => b.CreatedById).HasColumnName("created_by_id");
        builder.Property(b => b.CompletedAt).HasColumnName("completed_at");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => new { b.ClientId, b.Purpose, b.Status });
        builder.HasIndex(b => b.LoanId);
        builder.HasIndex(b => b.SessionId).IsUnique();
    }
}
