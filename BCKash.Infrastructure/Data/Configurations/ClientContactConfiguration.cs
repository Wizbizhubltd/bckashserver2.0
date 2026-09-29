using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> builder)
    {
        builder.ToTable("client_contacts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(191).IsRequired();
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(191);
        builder.Property(c => c.Address).HasColumnName("address");
        builder.Property(c => c.Relationship).HasColumnName("relationship").HasMaxLength(100);
        builder.Property(c => c.Occupation).HasColumnName("occupation").HasMaxLength(100);
        builder.Property(c => c.Gender).HasColumnName("gender").HasMaxLength(20);
        builder.Property(c => c.Photo).HasColumnName("photo").HasMaxLength(500);
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.ClientId, c.Kind });
    }
}
