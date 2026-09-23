using BCKash.Application.PayrollProcessing;
using BCKash.Domain.Payroll;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.PayrollProcessing;

public class PayrollService : IPayrollService
{
    private readonly BCKashDbContext _db;
    private readonly IPayrollGlPostingService _glPostingService;

    public PayrollService(BCKashDbContext db, IPayrollGlPostingService glPostingService)
    {
        _db = db;
        _glPostingService = glPostingService;
    }

    public async Task<PayrollRunResult> RunAsync(BCKash.Domain.Payroll.Payroll payroll, CancellationToken cancellationToken = default)
    {
        if (payroll.GrossAmount <= 0)
        {
            return new PayrollRunResult(PayrollRunOutcome.InvalidAmount);
        }

        List<PayrollTemplateMeta> lineItems = [];
        if (payroll.PayrollTemplateId is int templateId)
        {
            if (!await _db.PayrollTemplates.AnyAsync(t => t.Id == templateId, cancellationToken))
            {
                return new PayrollRunResult(PayrollRunOutcome.TemplateNotFound);
            }

            lineItems = await _db.PayrollTemplateMeta
                .Where(m => m.PayrollTemplateId == templateId)
                .ToListAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        payroll.Date ??= today;
        payroll.Year ??= payroll.Date.Value.Year.ToString();
        payroll.Month ??= payroll.Date.Value.Month.ToString();

        if (payroll.Recurring && payroll.RecurNextDate is null)
        {
            payroll.RecurNextDate = payroll.RecurStartDate ?? payroll.Date;
        }

        var computation = PayrollComputationRules.Compute(payroll.GrossAmount, lineItems.Select(ToLineInput).ToList());
        payroll.PaidAmount = computation.NetPay;

        _db.Payroll.Add(payroll);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var line in computation.Lines)
        {
            _db.PayrollMeta.Add(new PayrollMeta
            {
                PayrollId = payroll.Id,
                PayrollTemplateMetaId = line.PayrollTemplateMetaId,
                Value = line.Amount,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _glPostingService.PostPayrollRunAsync(payroll, cancellationToken);

        return new PayrollRunResult(PayrollRunOutcome.Success, payroll);
    }

    public async Task<int> GenerateDueRecurringAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await _db.Payroll
            .Where(p => p.Recurring && p.RecurNextDate != null && p.RecurNextDate <= today)
            .Where(p => p.RecurEndDate == null || p.RecurNextDate <= p.RecurEndDate)
            .ToListAsync(cancellationToken);

        var generated = 0;
        foreach (var template in due)
        {
            var occurrenceDate = template.RecurNextDate!.Value;

            var occurrence = new BCKash.Domain.Payroll.Payroll
            {
                PayrollTemplateId = template.PayrollTemplateId,
                GlAccountExpenseId = template.GlAccountExpenseId,
                GlAccountAssetId = template.GlAccountAssetId,
                UserId = template.UserId,
                OfficeId = template.OfficeId,
                EmployeeName = template.EmployeeName,
                BusinessName = template.BusinessName,
                PaymentMethod = template.PaymentMethod,
                PaymentTypeId = template.PaymentTypeId,
                BankName = template.BankName,
                AccountNumber = template.AccountNumber,
                Description = template.Description,
                Comments = template.Comments,
                GrossAmount = template.GrossAmount,
                Date = occurrenceDate,
                Recurring = false,
            };

            var result = await RunAsync(occurrence, cancellationToken);
            if (result.Outcome == PayrollRunOutcome.Success)
            {
                generated++;
            }

            var nextDate = PayrollRecurrenceRules.NextDate(occurrenceDate, template.RecurType, template.RecurFrequency);
            if (template.RecurEndDate is DateOnly end && nextDate > end)
            {
                template.Recurring = false;
                template.RecurNextDate = null;
            }
            else
            {
                template.RecurNextDate = nextDate;
            }
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return generated;
    }

    private static PayrollLineInput ToLineInput(PayrollTemplateMeta meta) => new(
        meta.Id,
        meta.Type ?? PayrollTemplateMetaType.Addition,
        meta.IsTax,
        meta.IsPercentage,
        meta.TaxOn,
        meta.DefaultValue ?? 0m);
}
