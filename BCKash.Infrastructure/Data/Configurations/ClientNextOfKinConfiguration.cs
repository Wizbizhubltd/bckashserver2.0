using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientNextOfKinConfiguration : IEntityTypeConfiguration<ClientNextOfKin>
{
    public void Configure(EntityTypeBuilder<ClientNextOfKin> builder)
    {
        builder.ToTable("client_next_of_kin");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(k => k.ClientId).HasColumnName("client_id");
        builder.Property(k => k.ClientRelationshipId).HasColumnName("client_relationship_id");
        builder.Property(k => k.Qualification).HasColumnName("qualification").HasMaxLength(191);
        builder.Property(k => k.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(k => k.MiddleName).HasColumnName("middle_name").HasMaxLength(191);
        builder.Property(k => k.LastName).HasColumnName("last_name").HasMaxLength(191);
        builder.Property(k => k.Ward).HasColumnName("ward").HasMaxLength(191);
        builder.Property(k => k.Street).HasColumnName("street").HasMaxLength(191);
        builder.Property(k => k.District).HasColumnName("district").HasMaxLength(191);
        builder.Property(k => k.Region).HasColumnName("region").HasMaxLength(191);
        builder.Property(k => k.Address).HasColumnName("address");
        builder.Property(k => k.Picture).HasColumnName("picture").HasMaxLength(191);
        builder.Property(k => k.Mobile).HasColumnName("mobile").HasMaxLength(191);
        builder.Property(k => k.Phone).HasColumnName("phone").HasMaxLength(191);
        builder.Property(k => k.Email).HasColumnName("email").HasMaxLength(191);

        builder.Property(k => k.Gender)
            .HasColumnName("gender")
            .HasConversion(
                g => g == Gender.Male ? "male" : g == Gender.Female ? "female" : g == Gender.Other ? "other" : g == Gender.Unspecified ? "unspecified" : null,
                s => s == "male" ? Gender.Male : s == "female" ? Gender.Female : s == "other" ? Gender.Other : s == "unspecified" ? Gender.Unspecified : (Gender?)null)
            .HasMaxLength(20);

        builder.Property(k => k.Notes).HasColumnName("notes");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(k => k.ClientId);

        builder.HasOne(k => k.Client)
            .WithMany(c => c.NextOfKin)
            .HasForeignKey(k => k.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(k => k.ClientRelationship)
            .WithMany(r => r.NextOfKin)
            .HasForeignKey(k => k.ClientRelationshipId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
