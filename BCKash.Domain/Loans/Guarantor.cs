using BCKash.Domain.Identity;
using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `guarantors` table (BRD §6.5).</summary>
public class Guarantor : IHasTimestamps
{
    public int Id { get; set; }

    /// <summary>References `countries.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CountryId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    /// <summary>References `savings.id` — outside this table group, kept as a plain scalar.</summary>
    public int? SavingsId { get; set; }

    public int? LoanId { get; set; }
    public int? LoanApplicationId { get; set; }

    public bool IsClient { get; set; }

    /// <summary>References `client_relationships.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientRelationshipId { get; set; }

    public decimal? Amount { get; set; }

    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Reuses <see cref="Gender"/> from BCKash.Domain.Identity — identical legal values (male/female/other/unspecified).</summary>
    public Gender? Gender { get; set; }

    public DateOnly? Dob { get; set; }
    public string? Street { get; set; }
    public string? Address { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Picture { get; set; }
    public string? Work { get; set; }
    public string? WorkAddress { get; set; }
    public bool LockFunds { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
    public LoanApplication? LoanApplication { get; set; }
}
