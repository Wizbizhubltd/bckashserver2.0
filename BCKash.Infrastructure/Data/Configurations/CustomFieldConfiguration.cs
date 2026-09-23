using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.ToTable("custom_fields");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(191);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(191);

        builder.Property(c => c.FieldType)
            .HasColumnName("field_type")
            .HasConversion(
                f => f == CustomFieldType.Number ? "number"
                    : f == CustomFieldType.Textfield ? "textfield"
                    : f == CustomFieldType.Date ? "date"
                    : f == CustomFieldType.Decimal ? "decimal"
                    : f == CustomFieldType.Textarea ? "textarea"
                    : f == CustomFieldType.Checkbox ? "checkbox"
                    : f == CustomFieldType.Radiobox ? "radiobox"
                    : "select",
                s => s == "number" ? CustomFieldType.Number
                    : s == "textfield" ? CustomFieldType.Textfield
                    : s == "date" ? CustomFieldType.Date
                    : s == "decimal" ? CustomFieldType.Decimal
                    : s == "textarea" ? CustomFieldType.Textarea
                    : s == "checkbox" ? CustomFieldType.Checkbox
                    : s == "radiobox" ? CustomFieldType.Radiobox
                    : CustomFieldType.Select)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Required).HasColumnName("required");
        builder.Property(c => c.RadioBoxValues).HasColumnName("radio_box_values");
        builder.Property(c => c.CheckboxValues).HasColumnName("checkbox_values");
        builder.Property(c => c.SelectValues).HasColumnName("select_values");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}
