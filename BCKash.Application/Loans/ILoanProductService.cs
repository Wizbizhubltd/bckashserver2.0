using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanProductWriteOutcome
{
    Success,
    NotFound,

    /// <summary>One of the min ≤ default ≤ max triads (principal/term/interest rate) is violated — FR-LN-2.</summary>
    InvalidRange,

    /// <summary>Delete blocked — a Loan or LoanApplication already references this product (FR-LN-1: deactivate instead).</summary>
    InUse,
}

public record LoanProductWriteResult(LoanProductWriteOutcome Outcome, LoanProduct? Product = null);

/// <summary>Loan product CRUD plus FR-LN-1/FR-LN-2. Mirrors IOfficeService/IGroupService's shape.</summary>
public interface ILoanProductService
{
    Task<LoanProductWriteResult> CreateAsync(LoanProduct product, CancellationToken cancellationToken = default);

    Task<LoanProductWriteResult> UpdateAsync(int id, LoanProduct updated, CancellationToken cancellationToken = default);

    Task<LoanProductWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<LoanProductWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task<LoanProductWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
