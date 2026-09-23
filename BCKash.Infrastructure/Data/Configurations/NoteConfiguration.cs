using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(n => n.ReferenceId).HasColumnName("reference_id");

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasConversion(
                t => t == ReferenceEntityType.Client ? "client" : t == ReferenceEntityType.Loan ? "loan" : t == ReferenceEntityType.Group ? "group" : t == ReferenceEntityType.Savings ? "savings" : t == ReferenceEntityType.Identification ? "identification" : t == ReferenceEntityType.Shares ? "shares" : t == ReferenceEntityType.Repayment ? "repayment" : null,
                s => s == "client" ? ReferenceEntityType.Client : s == "loan" ? ReferenceEntityType.Loan : s == "group" ? ReferenceEntityType.Group : s == "savings" ? ReferenceEntityType.Savings : s == "identification" ? ReferenceEntityType.Identification : s == "shares" ? ReferenceEntityType.Shares : s == "repayment" ? ReferenceEntityType.Repayment : (ReferenceEntityType?)null)
            .HasMaxLength(20);

        builder.Property(n => n.CreatedById).HasColumnName("created_by_id");
        builder.Property(n => n.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(n => n.Notes).HasColumnName("notes");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(n => new { n.Type, n.ReferenceId });
    }
}
