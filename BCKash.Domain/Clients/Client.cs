using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Maps the legacy `clients` table (BRD §6.3). The only entity in this table group that
/// implements <see cref="IAuditable"/> per FR-SEC-6.
/// </summary>
public class Client : IHasTimestamps, ISoftDelete, IAuditable
{
    public int Id { get; set; }

    /// <summary>Legacy `client_id` column — a carried-over id from a prior system/migration, not a self-reference.</summary>
    public int? LegacyClientId { get; set; }

    public string? Bvn { get; set; }
    public int? CountryId { get; set; }
    public int? OfficeId { get; set; }
    public int? UserId { get; set; }
    public int? StaffId { get; set; }
    public int? ReferredById { get; set; }
    public string? AccountNo { get; set; }
    public string? OldAccountNo { get; set; }
    public string? ExternalId { get; set; }
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? IncorporationNumber { get; set; }
    public string? DisplayName { get; set; }
    public string? Picture { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Gender? Gender { get; set; }
    public ClientType? ClientType { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Pending;
    public MaritalStatus? MaritalStatus { get; set; }
    public DateOnly? Dob { get; set; }
    public string? Street { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public DateOnly? JoinedDate { get; set; }
    public DateOnly? ActivatedDate { get; set; }
    public DateOnly? ReactivatedDate { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public string? DeclinedReason { get; set; }
    public string? ClosedReason { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public int? CreatedById { get; set; }
    public string? InactiveReason { get; set; }
    public DateOnly? InactiveDate { get; set; }
    public int? InactiveById { get; set; }
    public int? ActivatedById { get; set; }
    public int? ReactivatedById { get; set; }
    public int? DeclinedById { get; set; }
    public int? ClosedById { get; set; }
    public string? Notes { get; set; }
    public string? Occupation { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }

    /// <summary>Where the client trades — the paper loan form asks for it separately from their home (<see cref="Address"/>).</summary>
    public string? BusinessAddress { get; set; }
    public string? Nationality { get; set; }

    // Onboarding (see ClientOnboardingRules): the BVN is checked against the verification provider,
    // and a client kept on details that differ from what the BVN returned is flagged high risk until
    // a super admin marks them safe. A high-risk client can't be approved.
    public DateTime? BvnVerifiedAt { get; set; }

    /// <summary>Whose details were kept at onboarding: <c>bvn</c> (the provider's) or <c>client</c> (as given).</summary>
    public string? BvnDetailsSource { get; set; }
    public bool IsHighRisk { get; set; }
    public string? HighRiskReason { get; set; }
    public int? HighRiskFlaggedById { get; set; }
    public DateTime? HighRiskFlaggedAt { get; set; }
    public int? HighRiskClearedById { get; set; }
    public DateTime? HighRiskClearedAt { get; set; }
    public string? HighRiskClearedNote { get; set; }

    /// <summary>
    /// When the client's face was enrolled — a liveness-checked capture (see <see cref="ClientBiometric"/>)
    /// that also becomes their profile picture and is what loan face matches are compared against.
    /// </summary>
    public DateTime? BiometricEnrolledAt { get; set; }

    /// <summary>
    /// The granted <see cref="ClientEditRequest"/> currently unlocking an approved client's
    /// documentation; cleared when the client is approved again.
    /// </summary>
    public int? EditPrivilegeRequestId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Office? Office { get; set; }
    public User? User { get; set; }

    public ICollection<ClientIdentification> ClientIdentifications { get; set; } = new List<ClientIdentification>();
    public ICollection<ClientNextOfKin> NextOfKin { get; set; } = new List<ClientNextOfKin>();
    public ICollection<ClientNextOfGuardian> NextOfGuardians { get; set; } = new List<ClientNextOfGuardian>();
    public ICollection<ClientUser> ClientUsers { get; set; } = new List<ClientUser>();
    public ICollection<GroupClient> GroupClients { get; set; } = new List<GroupClient>();
}
