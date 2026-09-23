using BCKash.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GlClosureConfiguration : IEntityTypeConfiguration<GlClosure>
{
    public void Configure(EntityTypeBuilder<GlClosure> builder)
    {
        builder.ToTable("gl_closures");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.OfficeId).HasColumnName("office_id");
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.ClosingDate).HasColumnName("closing_date").HasColumnType("date").IsRequired();
        builder.Property(c => c.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(c => c.GlReference).HasColumnName("gl_reference").HasMaxLength(191);
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.ReopenedAt).HasColumnName("reopened_at");
        builder.Property(c => c.ReopenedById).HasColumnName("reopened_by_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.OfficeId, c.ClosingDate });
    }
}
