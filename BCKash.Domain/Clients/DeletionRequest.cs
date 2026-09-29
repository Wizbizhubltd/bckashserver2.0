using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

public enum DeletionRequestStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// A request to delete an approved client, or a group with an approved member — neither can be
/// deleted directly. Raised with a reason in the office portal; a super admin approves or rejects it
/// in the control portal.
/// </summary>
public class DeletionRequest : IHasTimestamps, IAuditable
{
    public const string ClientEntity = "client";
    public const string GroupEntity = "group";

    public int Id { get; set; }

    /// <summary><see cref="ClientEntity"/> or <see cref="GroupEntity"/>.</summary>
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }

    /// <summary>The client's or group's name when the request was raised, kept after it's deleted.</summary>
    public string? EntityName { get; set; }
    public int? OfficeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DeletionRequestStatus Status { get; set; } = DeletionRequestStatus.Pending;
    public int? RequestedById { get; set; }
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
