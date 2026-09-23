using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

public enum OtherIncomeTypeWriteOutcome
{
    Success,
    NotFound,
    InUse,
}

public record OtherIncomeTypeWriteResult(OtherIncomeTypeWriteOutcome Outcome, OtherIncomeType? OtherIncomeType = null);

/// <summary>Other-income type ("product") CRUD — the GL-account configuration used by income approval (BR-EXP-3). Mirrors ILoanProductService's shape.</summary>
public interface IOtherIncomeTypeService
{
    Task<OtherIncomeTypeWriteResult> CreateAsync(OtherIncomeType incomeType, CancellationToken cancellationToken = default);

    Task<OtherIncomeTypeWriteResult> UpdateAsync(int id, OtherIncomeType updated, CancellationToken cancellationToken = default);

    Task<OtherIncomeTypeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
