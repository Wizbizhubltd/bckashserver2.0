using BCKash.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class OfficeTransactionConfiguration : IEntityTypeConfiguration<OfficeTransaction>
{
    public void Configure(EntityTypeBuilder<OfficeTransaction> builder)
    {
        builder.ToTable("office_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.FromOfficeId).HasColumnName("from_office_id");
        builder.Property(t => t.ToOfficeId).HasColumnName("to_office_id");
        builder.Property(t => t.CurrencyId).HasColumnName("currency_id");
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(65, 8);
        builder.Property(t => t.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(t => t.Notes).HasColumnName("notes");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.FromOfficeId);
        builder.HasIndex(t => t.ToOfficeId);
    }
}
