using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientIdentificationTypeConfiguration : IEntityTypeConfiguration<ClientIdentificationType>
{
    public void Configure(EntityTypeBuilder<ClientIdentificationType> builder)
    {
        builder.ToTable("client_identification_types");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
