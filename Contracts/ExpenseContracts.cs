using BCKash.Domain.Expenses;

namespace BCKash.Api.Contracts;

public record SaveExpenseTypeRequest(string? Name, int? GlAccountAssetId, int? GlAccountExpenseId, string? Notes);

public record ExpenseTypeResponse(int Id, string? Name, int? GlAccountAssetId, int? GlAccountExpenseId, string? Notes);

public record SaveExpenseRequest(
    int? OfficeId,
    int? ExpenseTypeId,
    string? Name,
    decimal Amount,
    DateOnly? Date,
    bool Recurring,
    string? RecurFrequency,
    DateOnly? RecurStartDate,
    DateOnly? RecurEndDate,
    ExpenseRecurType RecurType,
    string? Notes,
    string? Files);

public record ExpenseResponse(
    int Id,
    int? OfficeId,
    int? ExpenseTypeId,
    string? Name,
    decimal Amount,
    DateOnly? Date,
    bool Recurring,
    string? RecurFrequency,
    DateOnly? RecurStartDate,
    DateOnly? RecurEndDate,
    DateOnly? RecurNextDate,
    ExpenseRecurType RecurType,
    ApprovalStatus Status,
    int? ApprovedById,
    DateOnly? ApprovedDate,
    int? DeclinedById,
    DateOnly? DeclinedDate,
    string? Notes);

public record ApproveRequest(string? Notes);

public record SaveExpenseBudgetRequest(int? OfficeId, int? ExpenseTypeId, string? Name, string? Year, string? Month, DateOnly? Date, decimal? Amount, string? Notes);

public record ExpenseBudgetResponse(
    int Id,
    int? OfficeId,
    int? ExpenseTypeId,
    string? Name,
    string? Year,
    string? Month,
    DateOnly? Date,
    decimal? Amount,
    string? Notes,
    ApprovalStatus Status,
    int? ApprovedById,
    DateOnly? ApprovedDate,
    int? DeclinedById,
    DateOnly? DeclinedDate);

public record SaveOtherIncomeTypeRequest(string? Name, int? GlAccountAssetId, int? GlAccountIncomeId, string? Notes);

public record OtherIncomeTypeResponse(int Id, string? Name, int? GlAccountAssetId, int? GlAccountIncomeId, string? Notes);

public record SaveOtherIncomeRequest(int? OfficeId, int? OtherIncomeTypeId, string? Name, decimal Amount, DateOnly? Date, string? Notes, string? Files);

public record OtherIncomeResponse(
    int Id,
    int? OfficeId,
    int? OtherIncomeTypeId,
    string? Name,
    decimal Amount,
    DateOnly? Date,
    ApprovalStatus Status,
    int? ApprovedById,
    DateOnly? ApprovedDate,
    int? DeclinedById,
    DateOnly? DeclinedDate,
    string? Notes);
