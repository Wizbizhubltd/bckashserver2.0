using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.ToTable("custom_field_values");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.CustomFieldId).HasColumnName("custom_field_id");
        builder.Property(v => v.EntityType).HasColumnName("entity_type").HasMaxLength(191).IsRequired();
        builder.Property(v => v.EntityId).HasColumnName("entity_id");
        builder.Property(v => v.Value).HasColumnName("value");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(v => new { v.EntityType, v.EntityId });

        builder.HasOne(v => v.CustomField)
            .WithMany()
            .HasForeignKey(v => v.CustomFieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
