using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>
/// Maps the legacy `group_loan_allocation` table (BRD §6.5) — how a group loan's principal
/// is allocated across individual group members. Has its own surrogate `id` primary key,
/// not a composite-key join table.
/// </summary>
public class GroupLoanAllocation : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanId { get; set; }

    /// <summary>References `groups.id` — outside this table group, kept as a plain scalar.</summary>
    public int? GroupId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
}
