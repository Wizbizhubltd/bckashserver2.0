using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ActivationConfiguration : IEntityTypeConfiguration<Activation>
{
    public void Configure(EntityTypeBuilder<Activation> builder)
    {
        builder.ToTable("activations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Code).HasColumnName("code").HasMaxLength(191).IsRequired();
        builder.Property(a => a.Completed).HasColumnName("completed");
        builder.Property(a => a.CompletedAt).HasColumnName("completed_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
