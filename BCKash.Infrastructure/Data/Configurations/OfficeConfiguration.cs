using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("offices");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(o => o.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(o => o.ParentId).HasColumnName("parent_id");
        builder.Property(o => o.ExternalId).HasColumnName("external_id").HasMaxLength(191);
        builder.Property(o => o.OpeningDate).HasColumnName("opening_date").HasColumnType("date");
        builder.Property(o => o.Address).HasColumnName("address");
        builder.Property(o => o.Phone).HasColumnName("phone");
        builder.Property(o => o.Email).HasColumnName("email");
        builder.Property(o => o.Notes).HasColumnName("notes");
        builder.Property(o => o.ManagerId).HasColumnName("manager_id");
        builder.Property(o => o.Active).HasColumnName("active");
        builder.Property(o => o.DefaultOffice).HasColumnName("default_office");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(o => o.Parent)
            .WithMany()
            .HasForeignKey(o => o.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
