using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SavingsAccountConfiguration : IEntityTypeConfiguration<SavingsAccount>
{
    public void Configure(EntityTypeBuilder<SavingsAccount> builder)
    {
        builder.ToTable("savings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(s => s.ClientType)
            .HasColumnName("client_type")
            .HasConversion(
                c => c == SavingsClientType.Group ? "group" : "client",
                s => s == "group" ? SavingsClientType.Group : SavingsClientType.Client)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(s => s.OldClientId).HasColumnName("old_client_id").HasMaxLength(191);
        builder.Property(s => s.GroupId).HasColumnName("group_id");
        builder.Property(s => s.OfficeId).HasColumnName("office_id");
        builder.Property(s => s.FieldOfficerId).HasColumnName("field_officer_id");
        builder.Property(s => s.SavingsProductId).HasColumnName("savings_product_id");
        builder.Property(s => s.ExternalId).HasColumnName("external_id").HasMaxLength(191);
        builder.Property(s => s.AccountNumber).HasColumnName("account_number").HasMaxLength(191);
        builder.Property(s => s.OldAccountNumber).HasColumnName("old_account_number").HasMaxLength(191);
        builder.Property(s => s.CurrencyId).HasColumnName("currency_id");
        builder.Property(s => s.Decimals).HasColumnName("decimals").IsRequired();
        builder.Property(s => s.InterestRate).HasColumnName("interest_rate").HasPrecision(65, 4);
        builder.Property(s => s.AllowOverdraft).HasColumnName("allow_overdraft").IsRequired();
        builder.Property(s => s.MinimumBalance).HasColumnName("minimum_balance").HasPrecision(65, 4);
        builder.Property(s => s.OverdraftLimit).HasColumnName("overdraft_limit").HasPrecision(65, 4);

        builder.Property(s => s.InterestCompoundingPeriod)
            .HasColumnName("interest_compounding_period")
            .HasConversion(
                c => c == null ? null : c == InterestCompoundingPeriod.Daily ? "daily" : c == InterestCompoundingPeriod.Monthly ? "monthly" : c == InterestCompoundingPeriod.Quarterly ? "quarterly" : c == InterestCompoundingPeriod.Biannual ? "biannual" : "annually",
                s => s == null ? (InterestCompoundingPeriod?)null : s == "daily" ? InterestCompoundingPeriod.Daily : s == "monthly" ? InterestCompoundingPeriod.Monthly : s == "quarterly" ? InterestCompoundingPeriod.Quarterly : s == "biannual" ? InterestCompoundingPeriod.Biannual : InterestCompoundingPeriod.Annually)
            .HasMaxLength(20);

        builder.Property(s => s.InterestPostingPeriod)
            .HasColumnName("interest_posting_period")
            .HasConversion(
                p => p == null ? null : p == InterestPostingPeriod.Monthly ? "monthly" : p == InterestPostingPeriod.Quarterly ? "quarterly" : p == InterestPostingPeriod.Biannual ? "biannual" : "annually",
                s => s == null ? (InterestPostingPeriod?)null : s == "monthly" ? InterestPostingPeriod.Monthly : s == "quarterly" ? InterestPostingPeriod.Quarterly : s == "biannual" ? InterestPostingPeriod.Biannual : InterestPostingPeriod.Annually)
            .HasMaxLength(20);

        builder.Property(s => s.AllowTransferWithdrawalFee).HasColumnName("allow_transfer_withdrawal_fee").IsRequired();
        builder.Property(s => s.OpeningBalance).HasColumnName("opening_balance").HasPrecision(65, 4);
        builder.Property(s => s.AllowAdditionalCharges).HasColumnName("allow_additional_charges").IsRequired();

        builder.Property(s => s.YearDays)
            .HasColumnName("year_days")
            .HasConversion(
                y => y == SavingsYearDays.Days360 ? "360" : "365",
                s => s == "360" ? SavingsYearDays.Days360 : SavingsYearDays.Days365)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion(
                st => st == SavingsAccountStatus.Pending ? "pending" : st == SavingsAccountStatus.Approved ? "approved" : st == SavingsAccountStatus.Closed ? "closed" : st == SavingsAccountStatus.Declined ? "declined" : "withdrawn",
                s => s == "pending" ? SavingsAccountStatus.Pending : s == "approved" ? SavingsAccountStatus.Approved : s == "closed" ? SavingsAccountStatus.Closed : s == "declined" ? SavingsAccountStatus.Declined : SavingsAccountStatus.Withdrawn)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CreatedById).HasColumnName("created_by_id");
        builder.Property(s => s.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(s => s.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(s => s.ClosedById).HasColumnName("closed_by_id");
        builder.Property(s => s.DeclinedById).HasColumnName("declined_by_id");

        builder.Property(s => s.CreatedDate).HasColumnName("created_date").HasColumnType("date");
        builder.Property(s => s.ModifiedDate).HasColumnName("modified_date").HasColumnType("date");
        builder.Property(s => s.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(s => s.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(s => s.ClosedDate).HasColumnName("closed_date").HasColumnType("date");

        builder.Property(s => s.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(s => s.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.ApprovedNotes).HasColumnName("approved_notes");
        builder.Property(s => s.DeclinedNotes).HasColumnName("declined_notes");
        builder.Property(s => s.ClosedNotes).HasColumnName("closed_notes");

        builder.Property(s => s.Balance).HasColumnName("balance").HasPrecision(65, 4);
        builder.Property(s => s.Deposits).HasColumnName("deposits").HasPrecision(65, 4);
        builder.Property(s => s.InterestEarned).HasColumnName("interest_earned").HasPrecision(65, 4);
        builder.Property(s => s.InterestPosted).HasColumnName("interest_posted").HasPrecision(65, 4);
        builder.Property(s => s.InterestOverdraft).HasColumnName("interest_overdraft").HasPrecision(65, 4);
        builder.Property(s => s.Withdrawals).HasColumnName("withdrawals").HasPrecision(65, 4);
        builder.Property(s => s.Fees).HasColumnName("fees").HasPrecision(65, 4);
        builder.Property(s => s.Penalty).HasColumnName("penalty").HasPrecision(65, 4);

        builder.Property(s => s.StartInterestCalculationDate).HasColumnName("start_interest_calculation_date").HasColumnType("date");
        builder.Property(s => s.LastInterestCalculationDate).HasColumnName("last_interest_calculation_date").HasColumnType("date");
        builder.Property(s => s.NextInterestCalculationDate).HasColumnName("next_interest_calculation_date").HasColumnType("date");
        builder.Property(s => s.NextInterestPostingDate).HasColumnName("next_interest_posting_date").HasColumnType("date");
        builder.Property(s => s.LastInterestPostingDate).HasColumnName("last_interest_posting_date").HasColumnType("date");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.AccountNumber).IsUnique();
        builder.HasIndex(s => s.ClientId);
        builder.HasIndex(s => s.InterestRate);
        builder.HasIndex(s => s.Balance);
        builder.HasIndex(s => s.LastInterestPostingDate);

        builder.HasOne(s => s.SavingsProduct)
            .WithMany(p => p.SavingsAccounts)
            .HasForeignKey(s => s.SavingsProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
