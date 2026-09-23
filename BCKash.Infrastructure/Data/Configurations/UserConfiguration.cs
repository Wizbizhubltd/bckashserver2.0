using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(u => u.OfficeId).HasColumnName("office_id");
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(191).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password").HasMaxLength(191).IsRequired();
        builder.Property(u => u.LegacyPermissionsRaw).HasColumnName("permissions");
        builder.Property(u => u.LastLogin).HasColumnName("last_login");
        builder.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(191);
        builder.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(191);

        builder.Property(u => u.Gender)
            .HasColumnName("gender")
            .HasConversion(
                g => g == Gender.Male ? "male" : g == Gender.Female ? "female" : g == Gender.Other ? "other" : "unspecified",
                s => s == "male" ? Gender.Male : s == "female" ? Gender.Female : s == "other" ? Gender.Other : Gender.Unspecified)
            .HasMaxLength(20);

        builder.Property(u => u.EnableGoogle2fa).HasColumnName("enable_google2fa");
        builder.Property(u => u.Blocked).HasColumnName("blocked");
        builder.Property(u => u.Google2faSecret).HasColumnName("google2fa_secret");
        builder.Property(u => u.Address).HasColumnName("address");
        builder.Property(u => u.Notes).HasColumnName("notes");
        builder.Property(u => u.TimeLimit).HasColumnName("time_limit");
        builder.Property(u => u.FromTime).HasColumnName("from_time").HasMaxLength(191);
        builder.Property(u => u.ToTime).HasColumnName("to_time").HasMaxLength(191);
        builder.Property(u => u.AccessDays).HasColumnName("access_days");

        builder.Property(u => u.UserClass)
            .HasColumnName("user_class")
            .HasConversion(
                c => c == Domain.Identity.UserClass.Initiator ? "initiator"
                    : c == Domain.Identity.UserClass.Authorizer ? "authorizer"
                    : c == Domain.Identity.UserClass.Reviewer ? "reviewer"
                    : null,
                s => s == "initiator" ? Domain.Identity.UserClass.Initiator
                    : s == "authorizer" ? Domain.Identity.UserClass.Authorizer
                    : s == "reviewer" ? Domain.Identity.UserClass.Reviewer
                    : (Domain.Identity.UserClass?)null)
            .HasMaxLength(20);

        builder.Property(u => u.CreatedById).HasColumnName("created_by_id");

        builder.Property(u => u.OnboardingStatus)
            .HasColumnName("onboarding_status")
            .HasConversion(
                s => s == UserOnboardingStatus.Pending ? "pending" : s == UserOnboardingStatus.Approved ? "approved" : "declined",
                s => s == "pending" ? UserOnboardingStatus.Pending : s == "approved" ? UserOnboardingStatus.Approved : UserOnboardingStatus.Declined)
            .HasMaxLength(20)
            .IsRequired()
            // Explicit, not left to EF's scaffolding default (the CLR default for the converted
            // *string* column is "", not the C# enum default) — every pre-existing row must
            // backfill to Approved, or the string→enum conversion's unmatched-value fallback
            // (Declined) would silently lock every current user out.
            .HasDefaultValue(UserOnboardingStatus.Approved);

        builder.Property(u => u.OnboardingApprovedById).HasColumnName("onboarding_approved_by_id");
        builder.Property(u => u.OnboardingApprovedDate).HasColumnName("onboarding_approved_date").HasColumnType("date");
        builder.Property(u => u.OnboardingDeclinedById).HasColumnName("onboarding_declined_by_id");
        builder.Property(u => u.OnboardingDeclinedDate).HasColumnName("onboarding_declined_date").HasColumnType("date");
        builder.Property(u => u.OnboardingDeclinedReason).HasColumnName("onboarding_declined_reason");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("users_email_unique");

        builder.HasOne(u => u.Office)
            .WithMany()
            .HasForeignKey(u => u.OfficeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
