using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Communications;

public class CommunicationCampaignService : ICommunicationCampaignService
{
    private readonly BCKashDbContext _db;
    private readonly ICampaignRecipientService _recipientService;
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;

    public CommunicationCampaignService(
        BCKashDbContext db, ICampaignRecipientService recipientService, ISmsSender smsSender, IEmailSender emailSender)
    {
        _db = db;
        _recipientService = recipientService;
        _smsSender = smsSender;
        _emailSender = emailSender;
    }

    public async Task<CampaignWriteResult> CreateAsync(CommunicationCampaign campaign, CancellationToken cancellationToken = default)
    {
        if (campaign.RecurrenceType == CampaignRecurrenceType.Schedule && campaign.NextRunDate is null)
        {
            campaign.NextRunDate = campaign.ReportStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        }

        _db.CommunicationCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return new CampaignWriteResult(CampaignWriteOutcome.Success, campaign);
    }

    public async Task<CampaignWriteResult> UpdateAsync(int id, CommunicationCampaign updated, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.CommunicationCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (campaign is null)
        {
            return new CampaignWriteResult(CampaignWriteOutcome.NotFound);
        }

        campaign.Type = updated.Type;
        campaign.Name = updated.Name;
        campaign.Description = updated.Description;
        campaign.ReportStartDate = updated.ReportStartDate;
        campaign.ReportStartTime = updated.ReportStartTime;
        campaign.RecurrenceType = updated.RecurrenceType;
        campaign.RecurFrequency = updated.RecurFrequency;
        campaign.RecurInterval = updated.RecurInterval;
        campaign.EmailRecipients = updated.EmailRecipients;
        campaign.EmailSubject = updated.EmailSubject;
        campaign.Message = updated.Message;
        campaign.EmailAttachmentFileFormat = updated.EmailAttachmentFileFormat;
        campaign.RecipientsCategory = updated.RecipientsCategory;
        campaign.ReportAttachment = updated.ReportAttachment;
        campaign.FromDay = updated.FromDay;
        campaign.ToDay = updated.ToDay;
        campaign.OfficeId = updated.OfficeId;
        campaign.LoanOfficerId = updated.LoanOfficerId;
        campaign.LoanStatus = updated.LoanStatus;
        campaign.LoanProductId = updated.LoanProductId;
        campaign.Active = updated.Active;

        await _db.SaveChangesAsync(cancellationToken);
        return new CampaignWriteResult(CampaignWriteOutcome.Success, campaign);
    }

    public async Task<CampaignWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.CommunicationCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (campaign is null)
        {
            return new CampaignWriteResult(CampaignWriteOutcome.NotFound);
        }

        _db.CommunicationCampaigns.Remove(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return new CampaignWriteResult(CampaignWriteOutcome.Success);
    }

    public async Task<CampaignWriteResult> RunAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.CommunicationCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (campaign is null)
        {
            return new CampaignWriteResult(CampaignWriteOutcome.NotFound);
        }

        var result = await SendAsync(campaign, cancellationToken);
        if (result != CampaignWriteOutcome.Success)
        {
            return new CampaignWriteResult(result, campaign);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new CampaignWriteResult(CampaignWriteOutcome.Success, campaign);
    }

    public async Task<int> GenerateDueRunsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await _db.CommunicationCampaigns
            .Where(c => c.Active && c.RecurrenceType == CampaignRecurrenceType.Schedule)
            .Where(c => c.NextRunDate != null && c.NextRunDate <= today)
            .ToListAsync(cancellationToken);

        var ran = 0;
        foreach (var campaign in due)
        {
            var outcome = await SendAsync(campaign, cancellationToken);
            if (outcome == CampaignWriteOutcome.Success)
            {
                ran++;
            }

            // Advances from the occurrence's own due date, not from today — so the next run
            // lands on a predictable date independent of when the admin happens to trigger this,
            // same as Phase 8's payroll/expense recurrence.
            campaign.NextRunDate = CampaignRecurrenceRules.NextDate(campaign.NextRunDate!.Value, campaign.RecurFrequency ?? CampaignRecurFrequency.Months, campaign.RecurInterval);
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ran;
    }

    private async Task<CampaignWriteOutcome> SendAsync(CommunicationCampaign campaign, CancellationToken cancellationToken)
    {
        var recipients = await _recipientService.ResolveAsync(campaign, cancellationToken);

        if (campaign.Type == CampaignType.Sms)
        {
            var gateway = await _db.SmsGateways.OrderByDescending(g => g.Id).FirstOrDefaultAsync(cancellationToken);
            if (gateway is null)
            {
                return CampaignWriteOutcome.GatewayNotFound;
            }

            foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Mobile)))
            {
                await _smsSender.SendAsync(gateway, recipient.Mobile!, campaign.Message ?? string.Empty, cancellationToken);
            }
        }
        else if (campaign.Type == CampaignType.Email)
        {
            if (string.IsNullOrWhiteSpace(campaign.EmailSubject))
            {
                return CampaignWriteOutcome.NotSendable;
            }

            foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)))
            {
                await _emailSender.SendAsync(recipient.Email!, campaign.EmailSubject!, campaign.Message ?? string.Empty, null, cancellationToken);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        campaign.LastRunDate = today;
        campaign.NumberOfRuns += 1;
        campaign.NumberOfRecipients = recipients.Count;
        campaign.Sent = true;

        return CampaignWriteOutcome.Success;
    }
}
