using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_applications` table (BRD §6.5).</summary>
public class LoanApplication : IHasTimestamps
{
    public int Id { get; set; }

    public LoanClientType ClientType { get; set; } = LoanClientType.Client;

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? UserId { get; set; }

    public int? LoanId { get; set; }
    public int? LoanPurposeId { get; set; }

    /// <summary>References `currencies.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CurrencyId { get; set; }

    /// <summary>References `offices.id` — outside this table group, kept as a plain scalar.</summary>
    public int? OfficeId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    /// <summary>References `groups.id` — outside this table group, kept as a plain scalar.</summary>
    public int? GroupId { get; set; }

    public int LoanProductId { get; set; }

    public decimal Amount { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>Legacy free-text list of guarantor ids; kept as raw text (no structured parsing here).</summary>
    public string? GuarantorIds { get; set; }

    public int? LoanTerm { get; set; }
    public FrequencyType? LoanTermType { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ApprovedById { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? DeclinedById { get; set; }

    public string? ApprovedNotes { get; set; }
    public string? DeclinedNotes { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
    public LoanProduct? LoanProduct { get; set; }
    public LoanPurpose? LoanPurpose { get; set; }
    public ICollection<Guarantor> Guarantors { get; set; } = new List<Guarantor>();
    public ICollection<Collateral> Collaterals { get; set; } = new List<Collateral>();
}
