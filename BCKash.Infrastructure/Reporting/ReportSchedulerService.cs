using BCKash.Application.Communications;
using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

public class ReportSchedulerService : IReportSchedulerService
{
    private readonly BCKashDbContext _db;
    private readonly IReportCatalogService _catalog;
    private readonly IReportExporter _exporter;
    private readonly IEmailSender _emailSender;

    public ReportSchedulerService(BCKashDbContext db, IReportCatalogService catalog, IReportExporter exporter, IEmailSender emailSender)
    {
        _db = db;
        _catalog = catalog;
        _exporter = exporter;
        _emailSender = emailSender;
    }

    public async Task<ReportScheduleWriteResult> CreateAsync(ReportScheduler schedule, CancellationToken cancellationToken = default)
    {
        if (schedule.RecurrenceType == ReportSchedulerRecurrenceType.Schedule && schedule.NextRunDate is null)
        {
            schedule.NextRunDate = schedule.ReportStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        }

        _db.ReportSchedulers.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.Success, schedule);
    }

    public async Task<ReportScheduleWriteResult> UpdateAsync(int id, ReportScheduler updated, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ReportSchedulers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (schedule is null)
        {
            return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.NotFound);
        }

        schedule.Description = updated.Description;
        schedule.ReportStartDate = updated.ReportStartDate;
        schedule.ReportStartTime = updated.ReportStartTime;
        schedule.RecurrenceType = updated.RecurrenceType;
        schedule.RecurFrequency = updated.RecurFrequency;
        schedule.RecurInterval = updated.RecurInterval;
        schedule.EmailRecipients = updated.EmailRecipients;
        schedule.EmailSubject = updated.EmailSubject;
        schedule.EmailMessage = updated.EmailMessage;
        schedule.EmailAttachmentFileFormat = updated.EmailAttachmentFileFormat;
        schedule.ReportCategory = updated.ReportCategory;
        schedule.ReportName = updated.ReportName;
        schedule.OfficeId = updated.OfficeId;
        schedule.LoanOfficerId = updated.LoanOfficerId;
        schedule.LoanStatus = updated.LoanStatus;
        schedule.LoanProductId = updated.LoanProductId;
        schedule.Active = updated.Active;

        await _db.SaveChangesAsync(cancellationToken);
        return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.Success, schedule);
    }

    public async Task<ReportScheduleWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ReportSchedulers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (schedule is null)
        {
            return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.NotFound);
        }

        _db.ReportSchedulers.Remove(schedule);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.Success);
    }

    public async Task<ReportScheduleWriteResult> RunAsync(int id, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ReportSchedulers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (schedule is null)
        {
            return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.NotFound);
        }

        await ExecuteAsync(schedule, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReportScheduleWriteResult(ReportScheduleWriteOutcome.Success, schedule);
    }

    public async Task<int> GenerateDueRunsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await _db.ReportSchedulers
            .Where(s => s.Active && s.RecurrenceType == ReportSchedulerRecurrenceType.Schedule)
            .Where(s => s.NextRunDate != null && s.NextRunDate <= today)
            .ToListAsync(cancellationToken);

        foreach (var schedule in due)
        {
            await ExecuteAsync(schedule, cancellationToken);
            // Advances from the occurrence's own due date, not from today — see
            // CommunicationCampaignService.GenerateDueRunsAsync's identical comment.
            schedule.NextRunDate = ReportSchedulerRecurrenceRules.NextDate(schedule.NextRunDate!.Value, schedule.RecurFrequency ?? ReportSchedulerRecurFrequency.Monthly, schedule.RecurInterval);
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    private async Task ExecuteAsync(ReportScheduler schedule, CancellationToken cancellationToken)
    {
        if (schedule.ReportName is null)
        {
            return;
        }

        var filter = new ReportFilter(
            OfficeId: int.TryParse(schedule.OfficeId, out var officeId) ? officeId : null,
            FromDate: schedule.StartDate,
            ToDate: schedule.EndDate,
            LoanOfficerId: int.TryParse(schedule.LoanOfficerId, out var loanOfficerId) ? loanOfficerId : null,
            LoanProductId: int.TryParse(schedule.LoanProductId, out var loanProductId) ? loanProductId : null);

        var result = await _catalog.RunAsync(schedule.ReportName.Value, filter, cancellationToken);
        var format = schedule.EmailAttachmentFileFormat ?? ReportSchedulerFileFormat.Pdf;
        var bytes = _exporter.Export(result, format);
        var fileName = $"{schedule.ReportName}.{_exporter.FileExtension(format)}";

        var recipients = (schedule.EmailRecipients ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var recipient in recipients)
        {
            await _emailSender.SendAsync(
                recipient,
                schedule.EmailSubject ?? schedule.ReportName.Value.ToString(),
                schedule.EmailMessage ?? string.Empty,
                [new EmailAttachment(fileName, bytes, _exporter.ContentType(format))],
                cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        schedule.LastRunDate = today;
        schedule.NumberOfRuns += 1;

        _db.ReportSchedulerRunHistories.Add(new ReportSchedulerRunHistory
        {
            ReportScheduleId = schedule.Id,
            ReportStartDate = today,
            Notes = $"Sent to {recipients.Length} recipient(s) as {format}.",
        });
    }
}
