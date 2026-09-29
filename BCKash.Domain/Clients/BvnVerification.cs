using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// One BVN lookup made during onboarding, with what the provider returned. Onboarding refers to it
/// by id, so the details a client is saved with come from the server's own lookup — never from
/// provider data the browser sends back.
/// </summary>
public class BvnVerification : IHasTimestamps
{
    public int Id { get; set; }
    public string Bvn { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }
    public int? RequestedById { get; set; }

    /// <summary>Set once a client is onboarded with this lookup, so it can't be reused for another.</summary>
    public int? ClientId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
