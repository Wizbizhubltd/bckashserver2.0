using BCKash.Domain.Identity;
using BCKash.SharedKernel;

namespace BCKash.Domain.Groups;

/// <summary>
/// Maps the legacy `group_users` table — links a group to a user account. Has its own surrogate
/// `id` primary key in the legacy schema (not a composite key).
/// </summary>
public class GroupUser : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public int? GroupId { get; set; }
    public int? UserId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Group? Group { get; set; }
    public User? User { get; set; }
}
