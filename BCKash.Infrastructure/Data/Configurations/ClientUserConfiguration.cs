using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientUserConfiguration : IEntityTypeConfiguration<ClientUser>
{
    public void Configure(EntityTypeBuilder<ClientUser> builder)
    {
        builder.ToTable("client_users");
        builder.HasKey(cu => cu.Id);

        builder.Property(cu => cu.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(cu => cu.CreatedById).HasColumnName("created_by_id");
        builder.Property(cu => cu.ClientId).HasColumnName("client_id");
        builder.Property(cu => cu.UserId).HasColumnName("user_id");
        builder.Property(cu => cu.CreatedAt).HasColumnName("created_at");
        builder.Property(cu => cu.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(cu => new { cu.ClientId, cu.UserId }).IsUnique();

        builder.HasOne(cu => cu.Client)
            .WithMany(c => c.ClientUsers)
            .HasForeignKey(cu => cu.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(cu => cu.User)
            .WithMany()
            .HasForeignKey(cu => cu.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
