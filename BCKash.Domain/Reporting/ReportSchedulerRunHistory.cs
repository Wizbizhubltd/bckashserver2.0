using BCKash.SharedKernel;

namespace BCKash.Domain.Reporting;

/// <summary>Maps the legacy `report_scheduler_run_history` table — execution log for a <see cref="ReportScheduler"/> (BRD §6.12).</summary>
public class ReportSchedulerRunHistory : IHasTimestamps
{
    public int Id { get; set; }
    public int? ReportScheduleId { get; set; }
    public DateOnly? ReportStartDate { get; set; }
    public string? ReportStartTime { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ReportScheduler? ReportSchedule { get; set; }
}
