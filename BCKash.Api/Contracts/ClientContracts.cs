using BCKash.Domain.Clients;
using BCKash.Domain.Identity;

namespace BCKash.Api.Contracts;

// ---- Clients ----

/// <summary>Slim projection for the paginated list screen (FR-CLI-5).</summary>
public record ClientListItemResponse(
    int Id, string? AccountNo, string? DisplayName, string? FirstName, string? MiddleName, string? LastName,
    string? Mobile, string? Bvn, int? OfficeId, int? StaffId, ClientStatus Status, ClientType? ClientType,
    DateOnly? JoinedDate);

public record ClientResponse(
    int Id, int? LegacyClientId, string? Bvn, int? CountryId, int? OfficeId, int? StaffId, int? ReferredById,
    string? AccountNo, string? OldAccountNo, string? ExternalId, string? Title, string? FirstName,
    string? MiddleName, string? LastName, string? FullName, string? IncorporationNumber, string? DisplayName,
    string? Picture, string? Mobile, string? Phone, string? Email, Gender? Gender, ClientType? ClientType,
    ClientStatus Status, MaritalStatus? MaritalStatus, DateOnly? Dob, string? Street, string? Ward,
    string? District, string? Region, string? Address, DateOnly? JoinedDate,
    DateOnly? ActivatedDate, int? ActivatedById, DateOnly? ReactivatedDate, int? ReactivatedById,
    DateOnly? DeclinedDate, int? DeclinedById, string? DeclinedReason,
    DateOnly? ClosedDate, int? ClosedById, string? ClosedReason,
    DateOnly? InactiveDate, int? InactiveById, string? InactiveReason,
    string? Notes, string? Occupation, string? PostalCode, string? Country, string? State, string? City);

public record CreateClientRequest(
    string? Bvn, int? CountryId, int? OfficeId, int? StaffId, int? ReferredById, string? ExternalId,
    string? Title, string? FirstName, string? MiddleName, string? LastName, string? FullName,
    string? IncorporationNumber, string? DisplayName, string? Picture, string? Mobile, string? Phone,
    string? Email, Gender? Gender, ClientType? ClientType, MaritalStatus? MaritalStatus, DateOnly? Dob,
    string? Street, string? Ward, string? District, string? Region, string? Address, DateOnly? JoinedDate,
    string? Occupation, string? PostalCode, string? Country, string? State, string? City);

public record UpdateClientRequest(
    string? Bvn, int? CountryId, int? OfficeId, int? StaffId, int? ReferredById, string? ExternalId,
    string? Title, string? FirstName, string? MiddleName, string? LastName, string? FullName,
    string? IncorporationNumber, string? DisplayName, string? Picture, string? Mobile, string? Phone,
    string? Email, Gender? Gender, ClientType? ClientType, MaritalStatus? MaritalStatus, DateOnly? Dob,
    string? Street, string? Ward, string? District, string? Region, string? Address, DateOnly? JoinedDate,
    string? Occupation, string? PostalCode, string? Country, string? State, string? City);

public record ActivateClientRequest(DateOnly? ActivatedDate);

/// <summary>Shared body shape for Deactivate/Decline/Close — all three require a reason.</summary>
public record ReasonRequest(string Reason);

// ---- Identifications, next-of-kin, next-of-guardian, notes ----

public record ClientIdentificationResponse(
    int Id, int? ClientId, int? ClientIdentificationTypeId, string? Name, bool Active, string? Notes);

public record SaveClientIdentificationRequest(
    int? ClientIdentificationTypeId, string? Name, bool Active, string? Notes);

public record ClientNextOfKinResponse(
    int Id, int? ClientId, int? ClientRelationshipId, string? Qualification, string? FirstName,
    string? MiddleName, string? LastName, string? Ward, string? Street, string? District, string? Region,
    string? Address, string? Mobile, string? Phone, string? Email, Gender? Gender, string? Notes);

public record SaveClientNextOfKinRequest(
    int? ClientRelationshipId, string? Qualification, string? FirstName, string? MiddleName, string? LastName,
    string? Ward, string? Street, string? District, string? Region, string? Address, string? Mobile,
    string? Phone, string? Email, Gender? Gender, string? Notes);

public record ClientNextOfGuardianResponse(
    int Id, int? ClientId, int? ClientRelationshipId, string? Qualification, string? FirstName,
    string? MiddleName, string? LastName, string? Ward, string? Street, string? District, string? Region,
    string? Address, string? Mobile, string? Phone, string? Email, Gender? Gender, string? Notes);

public record SaveClientNextOfGuardianRequest(
    int? ClientRelationshipId, string? Qualification, string? FirstName, string? MiddleName, string? LastName,
    string? Ward, string? Street, string? District, string? Region, string? Address, string? Mobile,
    string? Phone, string? Email, Gender? Gender, string? Notes);

public record NoteResponse(int Id, int? ReferenceId, ReferenceEntityType? Type, int? CreatedById, int? ModifiedById, string? Notes, DateTime? CreatedAt);

public record SaveNoteRequest(string? Notes);

// ---- Documents ----

/// <summary>No <c>Location</c> field — that's an internal storage key, never exposed to the SPA.</summary>
public record DocumentResponse(int Id, ReferenceEntityType? Type, int? RecordId, string? Name, string? Size, string? Notes, DateTime? CreatedAt);

// ---- Lookup tables: relationships, identification types, professions ----

public record ClientRelationshipResponse(int Id, string? Name);

public record SaveClientRelationshipRequest(string? Name);

public record ClientIdentificationTypeResponse(int Id, string? Name);

public record SaveClientIdentificationTypeRequest(string? Name);

public record ClientProfessionResponse(int Id, string? Name);

public record SaveClientProfessionRequest(string? Name);
