using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanApplicationWriteOutcome
{
    Success,
    NotFound,
    ProductNotFound,

    /// <summary>FR-LN-4 — the applied amount falls outside the product's min/max.</summary>
    AmountOutOfRange,

    /// <summary>FR-LN-4 — the applied term falls outside the product's min/max.</summary>
    TermOutOfRange,

    /// <summary>The application isn't in a status the requested action is valid from.</summary>
    InvalidTransition,

    ReasonRequired,
}

public record LoanApplicationWriteResult(LoanApplicationWriteOutcome Outcome, LoanApplication? Application = null);

/// <summary>
/// The loan application workflow (BR-LN-2, FR-LN-3 to FR-LN-6). Approve creates the linked
/// <see cref="Loan"/> header record (FR-LN-5) — see LoanApplicationService for the field mapping.
/// </summary>
public interface ILoanApplicationService
{
    Task<LoanApplicationWriteResult> CreateAsync(LoanApplication application, CancellationToken cancellationToken = default);

    /// <summary>Only while Status == Pending — mirrors ClientService/GroupService's "editable fields only" Update pattern.</summary>
    Task<LoanApplicationWriteResult> UpdateAsync(int id, LoanApplication updated, CancellationToken cancellationToken = default);

    /// <summary>FR-LN-5 — requires Status == Pending; creates/links the Loan record in LoanStatus.Pending, not yet disbursed.</summary>
    Task<LoanApplicationWriteResult> ApproveAsync(int id, decimal approvedAmount, string? notes, CancellationToken cancellationToken = default);

    /// <summary>FR-LN-6 — requires Status == Pending; terminal, the application cannot be resurrected.</summary>
    Task<LoanApplicationWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);
}
