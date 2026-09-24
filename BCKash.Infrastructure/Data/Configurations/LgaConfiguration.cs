using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LgaConfiguration : IEntityTypeConfiguration<Lga>
{
    public void Configure(EntityTypeBuilder<Lga> builder)
    {
        builder.ToTable("lgas");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(l => l.StateId).HasColumnName("state_id");
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.HasIndex(l => new { l.StateId, l.Name }).IsUnique().HasDatabaseName("lgas_state_id_name_unique");

        builder.HasOne(l => l.State)
            .WithMany(s => s.Lgas)
            .HasForeignKey(l => l.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
