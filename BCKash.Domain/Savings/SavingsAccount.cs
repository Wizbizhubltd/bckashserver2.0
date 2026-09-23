using BCKash.SharedKernel;

namespace BCKash.Domain.Savings;

/// <summary>Client this savings account belongs to — an individual client or a group (BRD §6.6).</summary>
public enum SavingsClientType
{
    Client,
    Group
}

/// <summary>Shared by <see cref="SavingsAccount"/> and <see cref="SavingsProduct"/> (legacy `interest_compounding_period`).</summary>
public enum InterestCompoundingPeriod
{
    Daily,
    Monthly,
    Quarterly,
    Biannual,
    Annually
}

/// <summary>Shared by <see cref="SavingsAccount"/> and <see cref="SavingsProduct"/> (legacy `interest_posting_period`).</summary>
public enum InterestPostingPeriod
{
    Monthly,
    Quarterly,
    Biannual,
    Annually
}

/// <summary>Shared by <see cref="SavingsAccount"/> and <see cref="SavingsProduct"/> (legacy `year_days`, values '360'/'365').</summary>
public enum SavingsYearDays
{
    Days360,
    Days365
}

/// <summary>Lifecycle status of a savings account (legacy `savings.status`).</summary>
public enum SavingsAccountStatus
{
    Pending,
    Approved,
    Closed,
    Declined,
    Withdrawn
}

/// <summary>
/// Maps the legacy `savings` table (BRD §6.6). Named <c>SavingsAccount</c> rather than
/// <c>Saving</c>/<c>Savings</c> since it represents a single conceptual savings account.
/// FKs to tables outside this table group (client_id, office_id, field_officer_id,
/// group_id, currency_id, created_by_id, etc.) are kept as plain scalar columns per
/// the Phase 0 data-model baseline — no navigation is added for those.
/// </summary>
public class SavingsAccount : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public SavingsClientType ClientType { get; set; } = SavingsClientType.Client;
    public int ClientId { get; set; }
    public string? OldClientId { get; set; }
    public int? GroupId { get; set; }
    public int? OfficeId { get; set; }
    public int? FieldOfficerId { get; set; }
    public int? SavingsProductId { get; set; }
    public string? ExternalId { get; set; }
    public string? AccountNumber { get; set; }
    public string? OldAccountNumber { get; set; }
    public int? CurrencyId { get; set; }
    public int Decimals { get; set; } = 2;
    public decimal? InterestRate { get; set; }
    public bool AllowOverdraft { get; set; }
    public decimal? MinimumBalance { get; set; }
    public decimal? OverdraftLimit { get; set; }
    public InterestCompoundingPeriod? InterestCompoundingPeriod { get; set; }
    public InterestPostingPeriod? InterestPostingPeriod { get; set; }
    public bool AllowTransferWithdrawalFee { get; set; }
    public decimal? OpeningBalance { get; set; }
    public bool AllowAdditionalCharges { get; set; }
    public SavingsYearDays YearDays { get; set; } = SavingsYearDays.Days365;
    public SavingsAccountStatus Status { get; set; } = SavingsAccountStatus.Pending;
    public int? CreatedById { get; set; }
    public int? ModifiedById { get; set; }
    public int? ApprovedById { get; set; }
    public int? ClosedById { get; set; }
    public int? DeclinedById { get; set; }
    public DateOnly? CreatedDate { get; set; }
    public DateOnly? ModifiedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }
    public string? Notes { get; set; }
    public string? ApprovedNotes { get; set; }
    public string? DeclinedNotes { get; set; }
    public string? ClosedNotes { get; set; }
    public decimal? Balance { get; set; }
    public decimal? Deposits { get; set; }
    public decimal? InterestEarned { get; set; }
    public decimal? InterestPosted { get; set; }
    public decimal? InterestOverdraft { get; set; }
    public decimal? Withdrawals { get; set; }
    public decimal? Fees { get; set; }
    public decimal? Penalty { get; set; }
    public DateOnly? StartInterestCalculationDate { get; set; }
    public DateOnly? LastInterestCalculationDate { get; set; }
    public DateOnly? NextInterestCalculationDate { get; set; }
    public DateOnly? NextInterestPostingDate { get; set; }
    public DateOnly? LastInterestPostingDate { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SavingsProduct? SavingsProduct { get; set; }
}
