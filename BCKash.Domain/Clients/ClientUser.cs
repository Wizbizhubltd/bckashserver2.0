using BCKash.Domain.Identity;
using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Maps the legacy `client_users` table — links a client to a user account (e.g. a portal login).
/// Has its own surrogate `id` primary key in the legacy schema (not a composite key).
/// </summary>
public class ClientUser : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public int? ClientId { get; set; }
    public int? UserId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public User? User { get; set; }
}
