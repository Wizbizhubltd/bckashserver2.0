using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(d => d.Type)
            .HasColumnName("type")
            .HasConversion(
                t => t == ReferenceEntityType.Client ? "client" : t == ReferenceEntityType.Loan ? "loan" : t == ReferenceEntityType.Group ? "group" : t == ReferenceEntityType.Savings ? "savings" : t == ReferenceEntityType.Identification ? "identification" : t == ReferenceEntityType.Shares ? "shares" : t == ReferenceEntityType.Repayment ? "repayment" : null,
                s => s == "client" ? ReferenceEntityType.Client : s == "loan" ? ReferenceEntityType.Loan : s == "group" ? ReferenceEntityType.Group : s == "savings" ? ReferenceEntityType.Savings : s == "identification" ? ReferenceEntityType.Identification : s == "shares" ? ReferenceEntityType.Shares : s == "repayment" ? ReferenceEntityType.Repayment : (ReferenceEntityType?)null)
            .HasMaxLength(20);

        builder.Property(d => d.RecordId).HasColumnName("record_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(d => d.Size).HasColumnName("size").HasMaxLength(191);
        builder.Property(d => d.Location).HasColumnName("location");
        builder.Property(d => d.Notes).HasColumnName("notes");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => new { d.Type, d.RecordId });
    }
}
