using BCKash.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(g => g.OldGroupId).HasColumnName("old_group_id").HasMaxLength(191);
        builder.Property(g => g.OfficeId).HasColumnName("office_id");
        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(g => g.AccountNo).HasColumnName("account_no").HasMaxLength(191);
        builder.Property(g => g.ExternalId).HasColumnName("external_id").HasMaxLength(191);
        builder.Property(g => g.StaffId).HasColumnName("staff_id");
        builder.Property(g => g.JoinedDate).HasColumnName("joined_date").HasColumnType("date");
        builder.Property(g => g.ActivatedDate).HasColumnName("activated_date").HasColumnType("date");
        builder.Property(g => g.ReactivatedDate).HasColumnName("reactivated_date").HasColumnType("date");
        builder.Property(g => g.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(g => g.DeclinedReason).HasColumnName("declined_reason");
        builder.Property(g => g.ClosedReason).HasColumnName("closed_reason");
        builder.Property(g => g.ClosedDate).HasColumnName("closed_date").HasColumnType("date");

        // New in Phase 3 — not in the legacy schema; see Group.cs's doc comment.
        builder.Property(g => g.InactiveReason).HasColumnName("inactive_reason");
        builder.Property(g => g.InactiveDate).HasColumnName("inactive_date").HasColumnType("date");
        builder.Property(g => g.InactiveById).HasColumnName("inactive_by_id");

        builder.Property(g => g.CreatedById).HasColumnName("created_by_id");
        builder.Property(g => g.ActivatedById).HasColumnName("activated_by_id");
        builder.Property(g => g.ReactivatedById).HasColumnName("reactivated_by_id");
        builder.Property(g => g.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(g => g.ClosedById).HasColumnName("closed_by_id");
        builder.Property(g => g.Mobile).HasColumnName("mobile").HasMaxLength(191);
        builder.Property(g => g.Phone).HasColumnName("phone").HasMaxLength(191);
        builder.Property(g => g.Email).HasColumnName("email").HasMaxLength(191);
        builder.Property(g => g.Street).HasColumnName("street").HasMaxLength(191);
        builder.Property(g => g.Ward).HasColumnName("ward").HasMaxLength(191);
        builder.Property(g => g.District).HasColumnName("district").HasMaxLength(191);
        builder.Property(g => g.Region).HasColumnName("region").HasMaxLength(191);
        builder.Property(g => g.Address).HasColumnName("address");
        builder.Property(g => g.Notes).HasColumnName("notes");

        builder.Property(g => g.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == GroupStatus.Pending ? "pending" : s == GroupStatus.Active ? "active" : s == GroupStatus.Inactive ? "inactive" : s == GroupStatus.Declined ? "declined" : "closed",
                s => s == "pending" ? GroupStatus.Pending : s == "active" ? GroupStatus.Active : s == "inactive" ? GroupStatus.Inactive : s == "declined" ? GroupStatus.Declined : GroupStatus.Closed)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.OfficeId);
        builder.HasIndex(g => g.StaffId);
        builder.HasIndex(g => g.Name);
        builder.HasIndex(g => g.AccountNo);

        builder.HasOne(g => g.Office)
            .WithMany()
            .HasForeignKey(g => g.OfficeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
