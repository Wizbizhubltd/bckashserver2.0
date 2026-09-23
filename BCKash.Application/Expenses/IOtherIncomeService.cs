using BCKash.Domain.Expenses;

namespace BCKash.Application.Expenses;

public enum OtherIncomeWriteOutcome
{
    Success,
    NotFound,
    TypeNotFound,
    InvalidTransition,
    ReasonRequired,
}

public record OtherIncomeWriteResult(OtherIncomeWriteOutcome Outcome, OtherIncome? OtherIncome = null);

/// <summary>
/// Other (non-lending) income CRUD plus its approval workflow (BR-EXP-3, FR-EXP-3) — same shape
/// as <see cref="IExpenseService"/>, minus recurrence (not required by FR-EXP-3). New records
/// always start Pending — GL posting only happens on approval, via
/// <see cref="IOtherIncomeGlPostingService"/>.
/// </summary>
public interface IOtherIncomeService
{
    Task<OtherIncomeWriteResult> CreateAsync(OtherIncome income, CancellationToken cancellationToken = default);

    Task<OtherIncomeWriteResult> UpdateAsync(int id, OtherIncome updated, CancellationToken cancellationToken = default);

    Task<OtherIncomeWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<OtherIncomeWriteResult> ApproveAsync(int id, string? notes, CancellationToken cancellationToken = default);

    Task<OtherIncomeWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);
}
