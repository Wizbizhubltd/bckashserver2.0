using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientEditRequestConfiguration : IEntityTypeConfiguration<ClientEditRequest>
{
    public void Configure(EntityTypeBuilder<ClientEditRequest> builder)
    {
        builder.ToTable("client_edit_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.ClientId).HasColumnName("client_id");
        builder.Property(r => r.Reason).HasColumnName("reason").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(r => r.RequestedById).HasColumnName("requested_by_id");
        builder.Property(r => r.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(r => r.ReviewNote).HasColumnName("review_note");
        builder.Property(r => r.FirstEditedAt).HasColumnName("first_edited_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.ClientId, r.Status });
        builder.HasIndex(r => r.Status);
    }
}
