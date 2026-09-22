using BCKash.Api.Contracts;
using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/report-schedules")]
[Authorize]
public class ReportSchedulesController : ControllerBase
{
    private const string ManagePolicy = "Permission:report-schedules.manage";

    private readonly BCKashDbContext _db;
    private readonly IReportSchedulerService _scheduleService;

    public ReportSchedulesController(BCKashDbContext db, IReportSchedulerService scheduleService)
    {
        _db = db;
        _scheduleService = scheduleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportScheduleResponse>>> List(CancellationToken cancellationToken)
    {
        var schedules = await _db.ReportSchedulers.OrderByDescending(s => s.Id).ToListAsync(cancellationToken);
        return Ok(schedules.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportScheduleResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var schedule = await _db.ReportSchedulers.FindAsync([id], cancellationToken);
        return schedule is null ? NotFound() : Ok(ToResponse(schedule));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveReportScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Schedule!.Id }, ToResponse(result.Schedule));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveReportScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            ReportScheduleWriteOutcome.Success => Ok(ToResponse(result.Schedule!)),
            ReportScheduleWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ReportScheduleWriteOutcome.Success => NoContent(),
            ReportScheduleWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-RPT-2 — runs one schedule immediately, regardless of its NextRunDate.</summary>
    [HttpPost("{id:int}/run")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Run(int id, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.RunAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ReportScheduleWriteOutcome.Success => Ok(ToResponse(result.Schedule!)),
            ReportScheduleWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-RPT-2 — admin-triggered since no scheduler exists in this codebase.</summary>
    [HttpPost("run-due")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RunDue(CancellationToken cancellationToken)
    {
        var count = await _scheduleService.GenerateDueRunsAsync(cancellationToken);
        return Ok(new { ran = count });
    }

    private static ReportScheduler ToEntity(SaveReportScheduleRequest request) => new()
    {
        Description = request.Description,
        ReportStartDate = request.ReportStartDate,
        ReportStartTime = request.ReportStartTime,
        RecurrenceType = request.RecurrenceType,
        RecurFrequency = request.RecurFrequency,
        RecurInterval = request.RecurInterval,
        EmailRecipients = request.EmailRecipients,
        EmailSubject = request.EmailSubject,
        EmailMessage = request.EmailMessage,
        EmailAttachmentFileFormat = request.EmailAttachmentFileFormat,
        ReportCategory = request.ReportCategory,
        ReportName = request.ReportName,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        OfficeId = request.OfficeId,
        LoanOfficerId = request.LoanOfficerId,
        LoanStatus = request.LoanStatus,
        LoanProductId = request.LoanProductId,
        Active = request.Active,
    };

    private static ReportScheduleResponse ToResponse(ReportScheduler s) => new(
        s.Id, s.Description, s.ReportCategory, s.ReportName, s.RecurrenceType, s.EmailAttachmentFileFormat,
        s.LastRunDate, s.NextRunDate, s.NumberOfRuns, s.Active, s.Status);
}
