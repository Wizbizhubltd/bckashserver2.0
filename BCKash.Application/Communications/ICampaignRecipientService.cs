using BCKash.Domain.Communications;

namespace BCKash.Application.Communications;

public record CampaignRecipient(int ClientId, string? Name, string? Mobile, string? Email);

/// <summary>
/// Recipient targeting for a campaign's configured category and filters (BR-COM-1, FR-COM-1).
/// See docs/phase9-communications-reporting-spec.md for the exact meaning locked in for each
/// category and filter, since FR-COM-1 names them but doesn't define their predicates.
/// </summary>
public interface ICampaignRecipientService
{
    Task<IReadOnlyList<CampaignRecipient>> ResolveAsync(CommunicationCampaign campaign, CancellationToken cancellationToken = default);
}
