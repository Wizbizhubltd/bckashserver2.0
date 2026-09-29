using BCKash.Domain.Clients;
using BCKash.Domain.Identity;

namespace BCKash.Api.Contracts;

// ---- Clients ----

/// <summary>Slim projection for the paginated list screen (FR-CLI-5).</summary>
public record ClientListItemResponse(
    int Id, string? AccountNo, string? DisplayName, string? FirstName, string? MiddleName, string? LastName,
    string? Mobile, string? Bvn, int? OfficeId, int? StaffId, ClientStatus Status, ClientType? ClientType,
    DateOnly? JoinedDate, bool IsHighRisk, int? GroupId = null, string? GroupName = null, bool FaceEnrolled = false);

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
    string? Notes, string? Occupation, string? PostalCode, string? Country, string? State, string? City,
    int? CreatedById, DateTime? CreatedAt,
    DateTime? BvnVerifiedAt, string? BvnDetailsSource,
    bool IsHighRisk, string? HighRiskReason, DateTime? HighRiskFlaggedAt, DateTime? HighRiskClearedAt, string? HighRiskClearedNote,
    bool HasPhoto,
    // Filled in on the single-client read (GET /clients/{id}) and after actions on it; null in lists.
    string? CreatedByName = null,
    string? ActivatedByName = null,
    string? OfficeName = null,
    ClientActionsResponse? Actions = null,
    bool? PendingDeletionRequest = null,
    /// <summary>What's still needed before a controller can approve the client, e.g. "1 more guarantor".</summary>
    IReadOnlyList<string>? ApprovalBlockers = null,
    /// <summary>The edit request waiting for a controller, or the granted one still open.</summary>
    ClientEditRequestResponse? CurrentEditRequest = null,
    /// <summary>The client's loan or application still open, e.g. "Loan LN123 is still running." — null when none. Blocks raising another.</summary>
    string? ActiveLoan = null,
    string? BusinessAddress = null,
    string? Nationality = null,
    /// <summary>The account officer (<c>StaffId</c>'s user), on the single-client read.</summary>
    string? StaffName = null,
    DateTime? BiometricEnrolledAt = null,
    /// <summary>Why a loan can't be raised for the client right now (pending, no face captured…) — null when it can. See LoanApplicantRules.</summary>
    string? LoanBlocker = null);

/// <summary>What the signed-in user may do with this client — drives the client page's action buttons.</summary>
public record ClientActionsResponse(
    bool CanEditDetails, bool CanApprove, bool CanDecline, bool CanDelete, bool CanRequestDeletion, bool CanMarkSafe,
    bool CanRequestEdit, bool EditPrivilegeOpen, bool CanReviewEditRequests);

/// <summary>A request to edit an approved client — see ClientEditRequest.</summary>
public record ClientEditRequestResponse(
    int Id, int ClientId, string? ClientName, string? ClientAccountNo, string? OfficeName,
    string Reason, string Status,
    int? RequestedById, string? RequestedByName, DateTime? CreatedAt,
    string? ReviewedByName, DateTime? ReviewedAt, string? ReviewNote,
    DateTime? FirstEditedAt, DateTime? CompletedAt);

public record ClientGroupMembershipResponse(int GroupId, string? GroupName, string? AccountNo, Domain.Groups.GroupStatus Status, string? Role, DateTime? JoinedAt);

/// <summary>One line of a client's audit trail, newest first.</summary>
public record ClientAuditEntryResponse(int? Id, DateTime? At, string? Action, string? Module, string? Notes, int? UserId, string? UserName);

public record MarkSafeRequest(string? Note);

public record CreateClientRequest(
    string? Bvn, int? CountryId, int? OfficeId, int? StaffId, int? ReferredById, string? ExternalId,
    string? Title, string? FirstName, string? MiddleName, string? LastName, string? FullName,
    string? IncorporationNumber, string? DisplayName, string? Picture, string? Mobile, string? Phone,
    string? Email, Gender? Gender, ClientType? ClientType, MaritalStatus? MaritalStatus, DateOnly? Dob,
    string? Street, string? Ward, string? District, string? Region, string? Address, DateOnly? JoinedDate,
    string? Occupation, string? PostalCode, string? Country, string? State, string? City,
    string? BusinessAddress = null, string? Nationality = null);

public record UpdateClientRequest(
    string? Bvn, int? CountryId, int? OfficeId, int? StaffId, int? ReferredById, string? ExternalId,
    string? Title, string? FirstName, string? MiddleName, string? LastName, string? FullName,
    string? IncorporationNumber, string? DisplayName, string? Picture, string? Mobile, string? Phone,
    string? Email, Gender? Gender, ClientType? ClientType, MaritalStatus? MaritalStatus, DateOnly? Dob,
    string? Street, string? Ward, string? District, string? Region, string? Address, DateOnly? JoinedDate,
    string? Occupation, string? PostalCode, string? Country, string? State, string? City,
    string? BusinessAddress = null, string? Nationality = null);

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
/// <summary><c>Category</c> is nin_slip, utility_bill or id_card (null on older uploads); <c>IdType</c> says which ID for an id_card.</summary>
public record DocumentResponse(
    int Id, ReferenceEntityType? Type, int? RecordId, string? Name, string? Size, string? Notes, DateTime? CreatedAt,
    string? Category = null, string? IdType = null, string? IdNumber = null, string? Label = null);

/// <summary>A guarantor or reference. Phone numbers are stored as +234….</summary>
public record ClientContactResponse(
    int Id, string Kind, string FullName, string? Phone, string? Email, string? Address, string? Relationship, string? Occupation, DateTime? CreatedAt,
    string? Gender = null, bool HasPhoto = false);

public record SaveClientContactRequest(string FullName, string? Phone, string? Email, string? Address, string? Relationship, string? Occupation, string? Gender = null);

/// <summary>What the client's printed loan form needs beyond the client record itself.</summary>
public record ClientLoanFormResponse(
    string? StaffName,
    decimal? ProposedLoanAmount,
    int? LoanTerm,
    Domain.Loans.FrequencyType? LoanTermType,
    string? LoanProductName,
    string? Nin,
    decimal? FormFee,
    int? GroupId,
    string? GroupName,
    IReadOnlyList<ClientLoanFormGroupMember> GroupMembers);

public record ClientLoanFormGroupMember(int ClientId, string? Name, string? Role);

// ---- Lookup tables: relationships, identification types, professions ----

public record ClientRelationshipResponse(int Id, string? Name);

public record SaveClientRelationshipRequest(string? Name);

public record ClientIdentificationTypeResponse(int Id, string? Name);

public record SaveClientIdentificationTypeRequest(string? Name);

public record ClientProfessionResponse(int Id, string? Name);

public record SaveClientProfessionRequest(string? Name);

// ---- Deletion requests ----

public record DeletionRequestResponse(
    int Id, string EntityType, int EntityId, string? EntityName, int? OfficeId, string? OfficeName,
    string Reason, DeletionRequestStatus Status,
    int? RequestedById, string? RequestedByName, DateTime? CreatedAt,
    int? ReviewedById, string? ReviewedByName, DateTime? ReviewedAt, string? ReviewNote);

public record ReviewDeletionRequest(string? Note);

// ---- Client loan savings ----

public record ClientSavingsEntryResponse(
    int Id, BCKash.Domain.Clients.ClientSavingsEntryType Type, decimal Amount, int? LoanId, string? LoanNumber, string? Notes, DateTime? CreatedAt, string? CreatedByName);

/// <summary>
/// What a client has saved from loan repayments. A withdrawal pays out <c>WithdrawalPayout</c> — the whole
/// balance, or less the early cash-out fee while a loan is running. <c>CanWithdraw</c>: the viewer may pay it out.
/// </summary>
public record ClientSavingsResponse(
    decimal Balance, bool HasRunningLoan, decimal EarlyWithdrawalFeeRate, decimal WithdrawalFee, decimal WithdrawalPayout, bool CanWithdraw, IReadOnlyList<ClientSavingsEntryResponse> Entries);

public record WithdrawSavingsRequest(string? Notes);

public record SavingsWithdrawalResponse(decimal Payout, decimal Fee, ClientSavingsResponse Savings);
