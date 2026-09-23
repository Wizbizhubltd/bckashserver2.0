using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientRelationshipConfiguration : IEntityTypeConfiguration<ClientRelationship>
{
    public void Configure(EntityTypeBuilder<ClientRelationship> builder)
    {
        builder.ToTable("client_relationships");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}
