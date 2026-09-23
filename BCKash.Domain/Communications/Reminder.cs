using BCKash.SharedKernel;

namespace BCKash.Domain.Communications;

/// <summary>Maps the legacy `reminders` table — per-user one-off task reminders (BRD §6.11).</summary>
public class Reminder : IHasTimestamps
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
