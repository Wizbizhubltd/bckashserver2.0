using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

/// <summary>Maps to the legacy `client_next_of_gaur` table (typo for "guardian" in the original schema).</summary>
public class ClientNextOfGuardianConfiguration : IEntityTypeConfiguration<ClientNextOfGuardian>
{
    public void Configure(EntityTypeBuilder<ClientNextOfGuardian> builder)
    {
        builder.ToTable("client_next_of_gaur");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(g => g.ClientId).HasColumnName("client_id");
        builder.Property(g => g.ClientRelationshipId).HasColumnName("client_relationship_id");
        builder.Property(g => g.Qualification).HasColumnName("qualification").HasMaxLength(191);
        builder.Property(g => g.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(g => g.MiddleName).HasColumnName("middle_name").HasMaxLength(191);
        builder.Property(g => g.LastName).HasColumnName("last_name").HasMaxLength(191);
        builder.Property(g => g.Ward).HasColumnName("ward").HasMaxLength(191);
        builder.Property(g => g.Street).HasColumnName("street").HasMaxLength(191);
        builder.Property(g => g.District).HasColumnName("district").HasMaxLength(191);
        builder.Property(g => g.Region).HasColumnName("region").HasMaxLength(191);
        builder.Property(g => g.Address).HasColumnName("address");
        builder.Property(g => g.Picture).HasColumnName("picture").HasMaxLength(191);
        builder.Property(g => g.Mobile).HasColumnName("mobile").HasMaxLength(191);
        builder.Property(g => g.Phone).HasColumnName("phone").HasMaxLength(191);
        builder.Property(g => g.Email).HasColumnName("email").HasMaxLength(191);

        builder.Property(g => g.Gender)
            .HasColumnName("gender")
            .HasConversion(
                x => x == Gender.Male ? "male" : x == Gender.Female ? "female" : x == Gender.Other ? "other" : x == Gender.Unspecified ? "unspecified" : null,
                s => s == "male" ? Gender.Male : s == "female" ? Gender.Female : s == "other" ? Gender.Other : s == "unspecified" ? Gender.Unspecified : (Gender?)null)
            .HasMaxLength(20);

        builder.Property(g => g.Notes).HasColumnName("notes");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(g => g.ClientId);

        builder.HasOne(g => g.Client)
            .WithMany(c => c.NextOfGuardians)
            .HasForeignKey(g => g.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(g => g.ClientRelationship)
            .WithMany(r => r.NextOfGuardians)
            .HasForeignKey(g => g.ClientRelationshipId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
