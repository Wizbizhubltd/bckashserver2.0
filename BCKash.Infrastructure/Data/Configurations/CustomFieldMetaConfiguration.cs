using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CustomFieldMetaConfiguration : IEntityTypeConfiguration<CustomFieldMeta>
{
    public void Configure(EntityTypeBuilder<CustomFieldMeta> builder)
    {
        builder.ToTable("custom_fields_meta");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(191);
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.CustomFieldId).HasColumnName("custom_field_id");
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(c => c.CustomField)
            .WithMany(c => c.Meta)
            .HasForeignKey(c => c.CustomFieldId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
