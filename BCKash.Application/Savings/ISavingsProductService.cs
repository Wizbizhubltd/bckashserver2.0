using BCKash.Domain.Savings;

namespace BCKash.Application.Savings;

public enum SavingsProductWriteOutcome
{
    Success,
    NotFound,

    /// <summary>Delete blocked — a SavingsAccount already references this product (FR-SAV-1: deactivate instead).</summary>
    InUse,
}

public record SavingsProductWriteResult(SavingsProductWriteOutcome Outcome, SavingsProduct? Product = null);

/// <summary>Savings product CRUD (BR-SAV-1/FR-SAV-1). Mirrors ILoanProductService's shape.</summary>
public interface ISavingsProductService
{
    Task<SavingsProductWriteResult> CreateAsync(SavingsProduct product, CancellationToken cancellationToken = default);

    Task<SavingsProductWriteResult> UpdateAsync(int id, SavingsProduct updated, CancellationToken cancellationToken = default);

    Task<SavingsProductWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<SavingsProductWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task<SavingsProductWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
