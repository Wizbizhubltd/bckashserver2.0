using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// A request to edit an approved client. Once a client is approved their documentation is locked;
/// the staff member who onboarded them asks for edit privilege with a reason, and a controller grants
/// or refuses it. The client stays Active until the first edit is saved, then goes back to Pending
/// until a controller approves them again — which closes the privilege.
/// </summary>
public class ClientEditRequest : IHasTimestamps, IAuditable
{
    public const string PendingStatus = "pending";

    /// <summary>Granted: the client's documentation can be edited until they're approved again.</summary>
    public const string ApprovedStatus = "approved";
    public const string RejectedStatus = "rejected";

    /// <summary>The client was edited and approved again, closing the privilege.</summary>
    public const string CompletedStatus = "completed";

    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = PendingStatus;
    public int? RequestedById { get; set; }
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>When the first edit under this privilege was saved (and the client went back to Pending).</summary>
    public DateTime? FirstEditedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
