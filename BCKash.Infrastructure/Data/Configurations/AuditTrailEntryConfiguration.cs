using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> builder)
    {
        builder.ToTable("audit_trail");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(a => a.OfficeId).HasColumnName("office_id");
        builder.Property(a => a.Module).HasColumnName("module").HasMaxLength(191);
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(191);
        builder.Property(a => a.Notes).HasColumnName("notes");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // No FK — legacy has none, and audit rows must survive the referenced user being deleted.
    }
}
