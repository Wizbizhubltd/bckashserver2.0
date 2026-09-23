using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientIdentificationConfiguration : IEntityTypeConfiguration<ClientIdentification>
{
    public void Configure(EntityTypeBuilder<ClientIdentification> builder)
    {
        builder.ToTable("client_identifications");
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(ci => ci.ClientId).HasColumnName("client_id");
        builder.Property(ci => ci.ClientIdentificationTypeId).HasColumnName("client_identification_type_id");
        builder.Property(ci => ci.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(ci => ci.Active).HasColumnName("active").HasColumnType("tinyint(4)").IsRequired();
        builder.Property(ci => ci.Notes).HasColumnName("notes");
        builder.Property(ci => ci.Attachment).HasColumnName("attachment");
        builder.Property(ci => ci.CreatedAt).HasColumnName("created_at");
        builder.Property(ci => ci.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(ci => ci.ClientId);

        builder.HasOne(ci => ci.Client)
            .WithMany(c => c.ClientIdentifications)
            .HasForeignKey(ci => ci.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(ci => ci.ClientIdentificationType)
            .WithMany(t => t.ClientIdentifications)
            .HasForeignKey(ci => ci.ClientIdentificationTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
