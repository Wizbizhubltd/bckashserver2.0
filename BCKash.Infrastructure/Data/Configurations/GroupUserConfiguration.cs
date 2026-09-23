using BCKash.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GroupUserConfiguration : IEntityTypeConfiguration<GroupUser>
{
    public void Configure(EntityTypeBuilder<GroupUser> builder)
    {
        builder.ToTable("group_users");
        builder.HasKey(gu => gu.Id);

        builder.Property(gu => gu.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(gu => gu.CreatedById).HasColumnName("created_by_id");
        builder.Property(gu => gu.GroupId).HasColumnName("group_id");
        builder.Property(gu => gu.UserId).HasColumnName("user_id");
        builder.Property(gu => gu.CreatedAt).HasColumnName("created_at");
        builder.Property(gu => gu.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(gu => gu.Group)
            .WithMany(g => g.GroupUsers)
            .HasForeignKey(gu => gu.GroupId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(gu => gu.User)
            .WithMany()
            .HasForeignKey(gu => gu.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
