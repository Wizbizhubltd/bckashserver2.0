using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Someone who vouches for a client: a guarantor (who stands behind the client's loans) or a reference.
/// A client needs at least <see cref="MinimumGuarantors"/> guarantors and <see cref="MinimumReferences"/>
/// reference before they can be approved.
/// </summary>
public class ClientContact : IHasTimestamps, IAuditable
{
    public const string GuarantorKind = "guarantor";
    public const string ReferenceKind = "reference";

    public const int MinimumGuarantors = 2;
    public const int MinimumReferences = 1;

    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary><see cref="GuarantorKind"/> or <see cref="ReferenceKind"/>.</summary>
    public string Kind { get; set; } = GuarantorKind;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>How they know the client, e.g. "Brother", "Employer".</summary>
    public string? Relationship { get; set; }
    public string? Occupation { get; set; }

    /// <summary>male, female or other.</summary>
    public string? Gender { get; set; }

    /// <summary>Storage key of the guarantor's passport photo (see IFileStorageService); null when none was uploaded.</summary>
    public string? Photo { get; set; }
    public int? CreatedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
