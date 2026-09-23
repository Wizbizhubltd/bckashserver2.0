using BCKash.Domain.Communications;

namespace BCKash.Application.Communications;

public enum CampaignWriteOutcome
{
    Success,
    NotFound,
    GatewayNotFound,

    /// <summary>An SMS campaign has no configured gateway, or an email campaign has no recipients/subject.</summary>
    NotSendable,
}

public record CampaignWriteResult(CampaignWriteOutcome Outcome, CommunicationCampaign? Campaign = null);

/// <summary>
/// Campaign CRUD, one-off/immediate send, and recurring send (BR-COM-1..3, FR-COM-1/FR-COM-2).
/// </summary>
public interface ICommunicationCampaignService
{
    Task<CampaignWriteResult> CreateAsync(CommunicationCampaign campaign, CancellationToken cancellationToken = default);

    Task<CampaignWriteResult> UpdateAsync(int id, CommunicationCampaign updated, CancellationToken cancellationToken = default);

    Task<CampaignWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves recipients and sends immediately, updating LastRunDate/NextRunDate/NumberOfRuns/
    /// NumberOfRecipients. An SMS campaign uses the single configured <see cref="Domain.Communications.SmsGateway"/>
    /// (the legacy schema has no per-campaign gateway selection — BCKash points every SMS
    /// campaign at whichever one gateway they've configured, per FR-COM-3); no gateway
    /// configured yields <see cref="CampaignWriteOutcome.GatewayNotFound"/>.
    /// </summary>
    Task<CampaignWriteResult> RunAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs every recurring campaign whose NextRunDate is due (&lt;= today), then advances it.
    /// No job scheduler exists anywhere in this codebase (same precedent as FR-SAV-4/FR-LN-25/
    /// Phase 8's payroll and expense recurrence) — this is an admin-triggered stand-in.
    /// </summary>
    Task<int> GenerateDueRunsAsync(CancellationToken cancellationToken = default);
}
