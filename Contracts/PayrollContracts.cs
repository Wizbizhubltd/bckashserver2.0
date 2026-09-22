using BCKash.Domain.Payroll;

namespace BCKash.Api.Contracts;

public record SavePayrollTemplateRequest(string? Name, string? Notes, string? Picture, bool? Active);

public record PayrollTemplateResponse(int Id, string? Name, string? Notes, string? Picture, bool? Active);

public record SavePayrollTemplateLineItemRequest(
    string? Name,
    PayrollTemplateMetaPosition? Position,
    PayrollTemplateMetaType? Type,
    bool IsDefault,
    bool IsTax,
    bool IsPercentage,
    PayrollTemplateMetaTaxOn? TaxOn,
    decimal? DefaultValue);

public record PayrollTemplateLineItemResponse(
    int Id,
    int PayrollTemplateId,
    string? Name,
    PayrollTemplateMetaPosition? Position,
    PayrollTemplateMetaType? Type,
    bool IsDefault,
    bool IsTax,
    bool IsPercentage,
    PayrollTemplateMetaTaxOn? TaxOn,
    decimal? DefaultValue);

public record RunPayrollRequest(
    int? PayrollTemplateId,
    int? GlAccountExpenseId,
    int? GlAccountAssetId,
    int? UserId,
    int? OfficeId,
    string? EmployeeName,
    string? BusinessName,
    string? PaymentMethod,
    string? PaymentTypeId,
    string? BankName,
    string? AccountNumber,
    string? Description,
    string? Comments,
    decimal GrossAmount,
    DateOnly? Date,
    bool Recurring,
    string? RecurFrequency,
    DateOnly? RecurStartDate,
    DateOnly? RecurEndDate,
    PayrollRecurType RecurType);

public record PayrollResponse(
    int Id,
    int? PayrollTemplateId,
    int? UserId,
    int? OfficeId,
    string? EmployeeName,
    string? BusinessName,
    decimal GrossAmount,
    decimal PaidAmount,
    DateOnly? Date,
    bool Recurring,
    DateOnly? RecurNextDate);
