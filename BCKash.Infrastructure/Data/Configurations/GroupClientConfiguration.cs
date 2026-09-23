using BCKash.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GroupClientConfiguration : IEntityTypeConfiguration<GroupClient>
{
    public void Configure(EntityTypeBuilder<GroupClient> builder)
    {
        builder.ToTable("group_clients");
        builder.HasKey(gc => gc.Id);

        builder.Property(gc => gc.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(gc => gc.GroupId).HasColumnName("group_id");
        builder.Property(gc => gc.ClientId).HasColumnName("client_id");
        builder.Property(gc => gc.OldGroupId).HasColumnName("old_group_id").HasMaxLength(255);
        builder.Property(gc => gc.OldClientId).HasColumnName("old_client_id").HasMaxLength(255);

        // New in Phase 3 — not in the legacy schema; see GroupClient.cs's doc comment.
        builder.Property(gc => gc.CreatedById).HasColumnName("created_by_id");
        builder.Property(gc => gc.RemovedAt).HasColumnName("removed_at");
        builder.Property(gc => gc.RemovedById).HasColumnName("removed_by_id");

        builder.Property(gc => gc.CreatedAt).HasColumnName("created_at");
        builder.Property(gc => gc.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(gc => gc.GroupId);
        builder.HasIndex(gc => gc.ClientId);

        builder.HasOne(gc => gc.Group)
            .WithMany(g => g.GroupClients)
            .HasForeignKey(gc => gc.GroupId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(gc => gc.Client)
            .WithMany(c => c.GroupClients)
            .HasForeignKey(gc => gc.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
