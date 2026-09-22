using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public class CampaignsController : ControllerBase
{
    private const string ManagePolicy = "Permission:campaigns.manage";
    private const string RunPolicy = "Permission:campaigns.run";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly ICommunicationCampaignService _campaignService;
    private readonly ICampaignRecipientService _recipientService;

    public CampaignsController(BCKashDbContext db, ICommunicationCampaignService campaignService, ICampaignRecipientService recipientService)
    {
        _db = db;
        _campaignService = campaignService;
        _recipientService = recipientService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CampaignResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.CommunicationCampaigns.AsQueryable();
        var totalCount = await query.CountAsync(cancellationToken);
        var campaigns = await query.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<CampaignResponse>(campaigns.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CampaignResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var campaign = await _db.CommunicationCampaigns.FindAsync([id], cancellationToken);
        return campaign is null ? NotFound() : Ok(ToResponse(campaign));
    }

    /// <summary>FR-COM-1 — preview who a not-yet-saved (or already-saved) recipient category/filter combination would target, against real client/loan data.</summary>
    [HttpPost("preview-recipients")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyList<CampaignRecipientResponse>>> PreviewRecipients(SaveCampaignRequest request, CancellationToken cancellationToken)
    {
        var recipients = await _recipientService.ResolveAsync(ToEntity(request), cancellationToken);
        return Ok(recipients.Select(r => new CampaignRecipientResponse(r.ClientId, r.Name, r.Mobile, r.Email)).ToList());
    }

    [HttpGet("{id:int}/recipients")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyList<CampaignRecipientResponse>>> Recipients(int id, CancellationToken cancellationToken)
    {
        var campaign = await _db.CommunicationCampaigns.FindAsync([id], cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        var recipients = await _recipientService.ResolveAsync(campaign, cancellationToken);
        return Ok(recipients.Select(r => new CampaignRecipientResponse(r.ClientId, r.Name, r.Mobile, r.Email)).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveCampaignRequest request, CancellationToken cancellationToken)
    {
        var result = await _campaignService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Campaign!.Id }, ToResponse(result.Campaign));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveCampaignRequest request, CancellationToken cancellationToken)
    {
        var result = await _campaignService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            CampaignWriteOutcome.Success => Ok(ToResponse(result.Campaign!)),
            CampaignWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _campaignService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            CampaignWriteOutcome.Success => NoContent(),
            CampaignWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-COM-2 — sends the campaign immediately.</summary>
    [HttpPost("{id:int}/run")]
    [Authorize(Policy = RunPolicy)]
    public async Task<IActionResult> Run(int id, CancellationToken cancellationToken)
    {
        var result = await _campaignService.RunAsync(id, cancellationToken);
        return result.Outcome switch
        {
            CampaignWriteOutcome.Success => Ok(ToResponse(result.Campaign!)),
            CampaignWriteOutcome.NotFound => NotFound(),
            CampaignWriteOutcome.GatewayNotFound => Problem(title: "No SMS gateway is configured.", statusCode: StatusCodes.Status400BadRequest),
            CampaignWriteOutcome.NotSendable => Problem(title: "This campaign is missing a required field (e.g. email subject) to be sent.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-COM-2 — admin-triggered since no scheduler exists in this codebase.</summary>
    [HttpPost("run-due")]
    [Authorize(Policy = RunPolicy)]
    public async Task<IActionResult> RunDue(CancellationToken cancellationToken)
    {
        var count = await _campaignService.GenerateDueRunsAsync(cancellationToken);
        return Ok(new { ran = count });
    }

    private static CommunicationCampaign ToEntity(SaveCampaignRequest request) => new()
    {
        Type = request.Type,
        Name = request.Name,
        Description = request.Description,
        ReportStartDate = request.ReportStartDate,
        ReportStartTime = request.ReportStartTime,
        RecurrenceType = request.RecurrenceType,
        RecurFrequency = request.RecurFrequency,
        RecurInterval = request.RecurInterval,
        EmailRecipients = request.EmailRecipients,
        EmailSubject = request.EmailSubject,
        Message = request.Message,
        EmailAttachmentFileFormat = request.EmailAttachmentFileFormat,
        RecipientsCategory = request.RecipientsCategory,
        ReportAttachment = request.ReportAttachment,
        FromDay = request.FromDay,
        ToDay = request.ToDay,
        OfficeId = request.OfficeId,
        LoanOfficerId = request.LoanOfficerId,
        LoanStatus = request.LoanStatus,
        LoanProductId = request.LoanProductId,
        Active = request.Active,
    };

    private static CampaignResponse ToResponse(CommunicationCampaign c) => new(
        c.Id, c.Type, c.Name, c.Description, c.RecipientsCategory, c.RecurrenceType,
        c.LastRunDate, c.NextRunDate, c.NumberOfRuns, c.NumberOfRecipients, c.Active, c.Sent, c.Status);
}
