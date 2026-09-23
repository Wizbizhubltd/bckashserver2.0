using BCKash.Domain.Payroll;

namespace BCKash.Application.PayrollProcessing;

public enum PayrollTemplateWriteOutcome
{
    Success,
    NotFound,
    LineItemNotFound,

    /// <summary>Delete blocked — a Payroll run already references this template.</summary>
    InUse,
}

public record PayrollTemplateWriteResult(PayrollTemplateWriteOutcome Outcome, PayrollTemplate? Template = null);

public record PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome Outcome, PayrollTemplateMeta? LineItem = null);

/// <summary>Payroll template CRUD, including its line items (FR-PAY-1: fixed/percentage/tax additions and deductions).</summary>
public interface IPayrollTemplateService
{
    Task<PayrollTemplateWriteResult> CreateAsync(PayrollTemplate template, CancellationToken cancellationToken = default);

    Task<PayrollTemplateWriteResult> UpdateAsync(int id, PayrollTemplate updated, CancellationToken cancellationToken = default);

    Task<PayrollTemplateWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<PayrollTemplateLineItemWriteResult> AddLineItemAsync(int templateId, PayrollTemplateMeta lineItem, CancellationToken cancellationToken = default);

    Task<PayrollTemplateLineItemWriteResult> UpdateLineItemAsync(int templateId, int lineItemId, PayrollTemplateMeta updated, CancellationToken cancellationToken = default);

    Task<PayrollTemplateLineItemWriteResult> RemoveLineItemAsync(int templateId, int lineItemId, CancellationToken cancellationToken = default);
}
