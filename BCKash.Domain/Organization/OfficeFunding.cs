using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>An office bank account. Exactly one active account per office is the default — where loan repayments are paid in, and where office funding is sent.</summary>
public class OfficeBankAccount : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool Active { get; set; } = true;
    public int? CreatedById { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public enum OfficeFundingStatus
{
    /// <summary>Sent by a super admin; not yet counted in the office's funds.</summary>
    PendingAcknowledgement,

    /// <summary>The office's manager confirmed receipt — the amount is now in the office's funds.</summary>
    Acknowledged,

    /// <summary>The office's manager says it wasn't received as described (reason + bank statement attached).</summary>
    Disputed,

    /// <summary>Withdrawn by a super admin before it was acknowledged.</summary>
    Cancelled,
}

/// <summary>Money a super admin sends an office. Counts towards the office's funds only once its manager acknowledges it.</summary>
public class OfficeFunding : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>The bank transfer reference — unique per office, so the same transfer can't be recorded twice.</summary>
    public string Reference { get; set; } = string.Empty;

    public DateOnly FundedOn { get; set; }

    /// <summary>The office's default account at the time — where the money was sent.</summary>
    public int? BankAccountId { get; set; }

    public string? Notes { get; set; }
    public OfficeFundingStatus Status { get; set; } = OfficeFundingStatus.PendingAcknowledgement;
    public int? FundedById { get; set; }

    public int? AcknowledgedById { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public int? DisputedById { get; set; }
    public DateTime? DisputedAt { get; set; }
    public string? DisputeReason { get; set; }
    public string? DisputeDocumentName { get; set; }
    public string? DisputeDocumentLocation { get; set; }

    public int? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>An office's available funds. <see cref="Balance"/> is a concurrency token so two disbursements can't overdraw it.</summary>
public class OfficeFund : IHasTimestamps
{
    public int OfficeId { get; set; }
    public decimal Balance { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public enum OfficeFundEntryType
{
    /// <summary>An acknowledged funding — money in.</summary>
    Funding,

    /// <summary>A loan disbursed from the office — money out.</summary>
    LoanDisbursement,
}

/// <summary>One movement on an office's funds (positive in, negative out) — the ledger behind <see cref="OfficeFund.Balance"/>.</summary>
public class OfficeFundEntry : IAuditable
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public OfficeFundEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public int? FundingId { get; set; }
    public int? LoanId { get; set; }
    public string? Description { get; set; }
    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum OfficeFundEventType
{
    FundingSent,
    FundingAcknowledged,
    FundingDisputed,
    FundingCancelled,
    BankAccountAdded,
    DefaultAccountChanged,
    BankAccountDeactivated,
    ManagerAssigned,
}

/// <summary>The business-operations activity log for an office — who did what, when, and why.</summary>
public class OfficeFundEvent
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public OfficeFundEventType Type { get; set; }
    public int? FundingId { get; set; }
    public int? BankAccountId { get; set; }
    public decimal? Amount { get; set; }
    public string? Comment { get; set; }
    public int? ActorId { get; set; }
    public DateTime CreatedAt { get; set; }
}
