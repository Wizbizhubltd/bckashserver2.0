using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class OfficeBankAccountConfiguration : IEntityTypeConfiguration<OfficeBankAccount>
{
    public void Configure(EntityTypeBuilder<OfficeBankAccount> builder)
    {
        builder.ToTable("office_bank_accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.OfficeId).HasColumnName("office_id");
        builder.Property(a => a.BankName).HasColumnName("bank_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.AccountName).HasColumnName("account_name").HasMaxLength(150).IsRequired();
        builder.Property(a => a.AccountNumber).HasColumnName("account_number").HasMaxLength(20).IsRequired();
        builder.Property(a => a.IsDefault).HasColumnName("is_default");
        builder.Property(a => a.Active).HasColumnName("active");
        builder.Property(a => a.CreatedById).HasColumnName("created_by_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(a => a.OfficeId);
    }
}

public class OfficeFundingConfiguration : IEntityTypeConfiguration<OfficeFunding>
{
    public void Configure(EntityTypeBuilder<OfficeFunding> builder)
    {
        builder.ToTable("office_fundings");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(f => f.OfficeId).HasColumnName("office_id");
        builder.Property(f => f.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(f => f.Reference).HasColumnName("reference").HasMaxLength(100).IsRequired();
        builder.Property(f => f.FundedOn).HasColumnName("funded_on").HasColumnType("date");
        builder.Property(f => f.BankAccountId).HasColumnName("bank_account_id");
        builder.Property(f => f.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.FundedById).HasColumnName("funded_by_id");
        builder.Property(f => f.AcknowledgedById).HasColumnName("acknowledged_by_id");
        builder.Property(f => f.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(f => f.DisputedById).HasColumnName("disputed_by_id");
        builder.Property(f => f.DisputedAt).HasColumnName("disputed_at");
        builder.Property(f => f.DisputeReason).HasColumnName("dispute_reason").HasMaxLength(2000);
        builder.Property(f => f.DisputeDocumentName).HasColumnName("dispute_document_name").HasMaxLength(255);
        builder.Property(f => f.DisputeDocumentLocation).HasColumnName("dispute_document_location").HasMaxLength(500);
        builder.Property(f => f.CancelledById).HasColumnName("cancelled_by_id");
        builder.Property(f => f.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(f => f.CancelReason).HasColumnName("cancel_reason").HasMaxLength(1000);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        // Status is a concurrency token: acknowledging, disputing and cancelling can't race each other.
        builder.Property(f => f.Status).IsConcurrencyToken();
        builder.HasIndex(f => new { f.OfficeId, f.Reference }).IsUnique().HasDatabaseName("office_fundings_reference_unique");
    }
}

public class OfficeFundConfiguration : IEntityTypeConfiguration<OfficeFund>
{
    public void Configure(EntityTypeBuilder<OfficeFund> builder)
    {
        builder.ToTable("office_funds");
        builder.HasKey(f => f.OfficeId);
        builder.Property(f => f.OfficeId).HasColumnName("office_id").ValueGeneratedNever();
        builder.Property(f => f.Balance).HasColumnName("balance").HasPrecision(65, 2).IsConcurrencyToken();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
    }
}

public class OfficeFundEntryConfiguration : IEntityTypeConfiguration<OfficeFundEntry>
{
    public void Configure(EntityTypeBuilder<OfficeFundEntry> builder)
    {
        builder.ToTable("office_fund_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OfficeId).HasColumnName("office_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(e => e.BalanceAfter).HasColumnName("balance_after").HasPrecision(65, 2);
        builder.Property(e => e.FundingId).HasColumnName("funding_id");
        builder.Property(e => e.LoanId).HasColumnName("loan_id");
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(255);
        builder.Property(e => e.CreatedById).HasColumnName("created_by_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(e => e.OfficeId);
        // A loan is only ever disbursed from its office's funds once.
        builder.HasIndex(e => e.LoanId).IsUnique().HasDatabaseName("office_fund_entries_loan_unique");
    }
}

public class OfficeFundEventConfiguration : IEntityTypeConfiguration<OfficeFundEvent>
{
    public void Configure(EntityTypeBuilder<OfficeFundEvent> builder)
    {
        builder.ToTable("office_fund_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OfficeId).HasColumnName("office_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.FundingId).HasColumnName("funding_id");
        builder.Property(e => e.BankAccountId).HasColumnName("bank_account_id");
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(2000);
        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(e => e.OfficeId);
    }
}
