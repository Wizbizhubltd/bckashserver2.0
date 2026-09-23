using BCKash.Domain.Communications;

namespace BCKash.Application.Communications;

public enum SmsGatewayWriteOutcome
{
    Success,
    NotFound,
}

public record SmsGatewayWriteResult(SmsGatewayWriteOutcome Outcome, SmsGateway? Gateway = null);

/// <summary>SMS gateway configuration CRUD (BR-COM-4, FR-COM-3).</summary>
public interface ISmsGatewayService
{
    Task<SmsGatewayWriteResult> CreateAsync(SmsGateway gateway, CancellationToken cancellationToken = default);

    Task<SmsGatewayWriteResult> UpdateAsync(int id, SmsGateway updated, CancellationToken cancellationToken = default);

    Task<SmsGatewayWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
