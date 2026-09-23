using BCKash.Domain.GeneralLedger;

namespace BCKash.Application.GeneralLedger;

public enum OfficeTransferOutcome
{
    Success,
    InvalidAmount,
    SameOffice,
    AccountNotFound,
    ClosurePeriod,
}

public record OfficeTransferResult(OfficeTransferOutcome Outcome, OfficeTransaction? Transaction = null);

/// <summary>
/// FR-GL-5: inter-office fund transfers, posted as a matched pair of journal entries against a
/// caller-chosen clearing account (offices have no GL account of their own to infer one from —
/// see docs/gl-posting-spec.md).
/// </summary>
public interface IOfficeTransferService
{
    Task<OfficeTransferResult> CreateAsync(
        int fromOfficeId, int toOfficeId, int? currencyId, decimal amount, int glAccountId, DateOnly date, string? notes,
        CancellationToken cancellationToken = default);
}
