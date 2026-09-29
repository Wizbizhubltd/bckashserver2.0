using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class DeletionRequestConfiguration : IEntityTypeConfiguration<DeletionRequest>
{
    public void Configure(EntityTypeBuilder<DeletionRequest> builder)
    {
        builder.ToTable("deletion_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.EntityType).HasColumnName("entity_type").HasMaxLength(20).IsRequired();
        builder.Property(r => r.EntityId).HasColumnName("entity_id");
        builder.Property(r => r.EntityName).HasColumnName("entity_name").HasMaxLength(191);
        builder.Property(r => r.OfficeId).HasColumnName("office_id");
        builder.Property(r => r.Reason).HasColumnName("reason").IsRequired();
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == DeletionRequestStatus.Approved ? "approved" : s == DeletionRequestStatus.Rejected ? "rejected" : "pending",
                s => s == "approved" ? DeletionRequestStatus.Approved : s == "rejected" ? DeletionRequestStatus.Rejected : DeletionRequestStatus.Pending)
            .HasMaxLength(20);
        builder.Property(r => r.RequestedById).HasColumnName("requested_by_id");
        builder.Property(r => r.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(r => r.ReviewNote).HasColumnName("review_note");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.EntityType, r.EntityId });
        builder.HasIndex(r => r.Status);
    }
}
