using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class BvnVerificationConfiguration : IEntityTypeConfiguration<BvnVerification>
{
    public void Configure(EntityTypeBuilder<BvnVerification> builder)
    {
        builder.ToTable("bvn_verifications");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.Bvn).HasColumnName("bvn").HasMaxLength(11).IsRequired();
        builder.Property(v => v.Found).HasColumnName("found");
        builder.Property(v => v.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(v => v.MiddleName).HasColumnName("middle_name").HasMaxLength(191);
        builder.Property(v => v.LastName).HasColumnName("last_name").HasMaxLength(191);
        builder.Property(v => v.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(v => v.BirthDate).HasColumnName("birth_date").HasMaxLength(20);
        builder.Property(v => v.Gender).HasColumnName("gender").HasMaxLength(20);
        builder.Property(v => v.RequestedById).HasColumnName("requested_by_id");
        builder.Property(v => v.ClientId).HasColumnName("client_id");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(v => v.Bvn);
    }
}
