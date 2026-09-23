using BCKash.Application.PayrollProcessing;
using BCKash.Domain.Payroll;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.PayrollProcessing;

public class PayrollTemplateService : IPayrollTemplateService
{
    private readonly BCKashDbContext _db;

    public PayrollTemplateService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<PayrollTemplateWriteResult> CreateAsync(PayrollTemplate template, CancellationToken cancellationToken = default)
    {
        _db.PayrollTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.Success, template);
    }

    public async Task<PayrollTemplateWriteResult> UpdateAsync(int id, PayrollTemplate updated, CancellationToken cancellationToken = default)
    {
        var template = await _db.PayrollTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.NotFound);
        }

        template.Name = updated.Name;
        template.Notes = updated.Notes;
        template.Picture = updated.Picture;
        template.Active = updated.Active;

        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.Success, template);
    }

    public async Task<PayrollTemplateWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _db.PayrollTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.NotFound);
        }

        var inUse = await _db.Payroll.AnyAsync(p => p.PayrollTemplateId == id, cancellationToken);
        if (inUse)
        {
            return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.InUse, template);
        }

        var lineItems = await _db.PayrollTemplateMeta.Where(m => m.PayrollTemplateId == id).ToListAsync(cancellationToken);
        _db.PayrollTemplateMeta.RemoveRange(lineItems);
        _db.PayrollTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateWriteResult(PayrollTemplateWriteOutcome.Success);
    }

    public async Task<PayrollTemplateLineItemWriteResult> AddLineItemAsync(int templateId, PayrollTemplateMeta lineItem, CancellationToken cancellationToken = default)
    {
        if (!await _db.PayrollTemplates.AnyAsync(t => t.Id == templateId, cancellationToken))
        {
            return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.NotFound);
        }

        lineItem.PayrollTemplateId = templateId;
        _db.PayrollTemplateMeta.Add(lineItem);
        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.Success, lineItem);
    }

    public async Task<PayrollTemplateLineItemWriteResult> UpdateLineItemAsync(int templateId, int lineItemId, PayrollTemplateMeta updated, CancellationToken cancellationToken = default)
    {
        var lineItem = await _db.PayrollTemplateMeta
            .FirstOrDefaultAsync(m => m.Id == lineItemId && m.PayrollTemplateId == templateId, cancellationToken);
        if (lineItem is null)
        {
            return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.LineItemNotFound);
        }

        lineItem.Name = updated.Name;
        lineItem.Position = updated.Position;
        lineItem.Type = updated.Type;
        lineItem.IsDefault = updated.IsDefault;
        lineItem.IsTax = updated.IsTax;
        lineItem.IsPercentage = updated.IsPercentage;
        lineItem.TaxOn = updated.TaxOn;
        lineItem.DefaultValue = updated.DefaultValue;

        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.Success, lineItem);
    }

    public async Task<PayrollTemplateLineItemWriteResult> RemoveLineItemAsync(int templateId, int lineItemId, CancellationToken cancellationToken = default)
    {
        var lineItem = await _db.PayrollTemplateMeta
            .FirstOrDefaultAsync(m => m.Id == lineItemId && m.PayrollTemplateId == templateId, cancellationToken);
        if (lineItem is null)
        {
            return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.LineItemNotFound);
        }

        _db.PayrollTemplateMeta.Remove(lineItem);
        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollTemplateLineItemWriteResult(PayrollTemplateWriteOutcome.Success);
    }
}
