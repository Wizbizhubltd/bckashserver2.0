using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(n => n.UserId).HasColumnName("user_id");
        builder.Property(n => n.OfficeId).HasColumnName("office_id");
        builder.Property(n => n.Kind).HasColumnName("kind").HasMaxLength(40).IsRequired();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body");
        builder.Property(n => n.Link).HasColumnName("link").HasMaxLength(200);
        builder.Property(n => n.EntityType).HasColumnName("entity_type").HasMaxLength(40).IsRequired();
        builder.Property(n => n.EntityId).HasColumnName("entity_id");
        builder.Property(n => n.NeedsAction).HasColumnName("needs_action");
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
        builder.Property(n => n.DoneAt).HasColumnName("done_at");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(n => new { n.UserId, n.DoneAt });
        builder.HasIndex(n => new { n.EntityType, n.EntityId });
    }
}

public class RepaymentSubmissionConfiguration : IEntityTypeConfiguration<RepaymentSubmission>
{
    public void Configure(EntityTypeBuilder<RepaymentSubmission> builder)
    {
        builder.ToTable("repayment_submissions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(s => s.LoanId).HasColumnName("loan_id");
        builder.Property(s => s.OfficeId).HasColumnName("office_id");
        builder.Property(s => s.Amount).HasColumnName("amount").HasPrecision(65, 4);
        builder.Property(s => s.PaymentTypeId).HasColumnName("payment_type_id");
        builder.Property(s => s.PaymentDate).HasColumnName("payment_date").HasColumnType("date");
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.SubmittedById).HasColumnName("submitted_by_id");
        builder.Property(s => s.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(s => s.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(s => s.DisputeReason).HasColumnName("dispute_reason");
        builder.Property(s => s.LoanTransactionId).HasColumnName("loan_transaction_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => new { s.LoanId, s.Status });
        builder.HasIndex(s => new { s.OfficeId, s.Status });
    }
}
