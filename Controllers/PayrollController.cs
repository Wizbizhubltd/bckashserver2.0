using BCKash.Api.Contracts;
using BCKash.Application.PayrollProcessing;
using BCKash.Domain.Payroll;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollController : ControllerBase
{
    private const string RunPolicy = "Permission:payroll.run";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IPayrollService _payrollService;

    public PayrollController(BCKashDbContext db, IPayrollService payrollService)
    {
        _db = db;
        _payrollService = payrollService;
    }

    [HttpGet("runs")]
    public async Task<ActionResult<PagedResult<PayrollResponse>>> List(
        [FromQuery] int? officeId,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Payroll.AsQueryable();
        if (officeId.HasValue)
        {
            query = query.Where(p => p.OfficeId == officeId);
        }

        if (userId.HasValue)
        {
            query = query.Where(p => p.UserId == userId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var runs = await query.OrderByDescending(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<PayrollResponse>(runs.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("runs/{id:int}")]
    public async Task<ActionResult<PayrollResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var run = await _db.Payroll.FindAsync([id], cancellationToken);
        return run is null ? NotFound() : Ok(ToResponse(run));
    }

    /// <summary>FR-PAY-2 — computes net pay from the template's line items and posts GL.</summary>
    [HttpPost("runs")]
    [Authorize(Policy = RunPolicy)]
    public async Task<IActionResult> Run(RunPayrollRequest request, CancellationToken cancellationToken)
    {
        var result = await _payrollService.RunAsync(ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            PayrollRunOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Payroll!.Id }, ToResponse(result.Payroll)),
            PayrollRunOutcome.TemplateNotFound => Problem(title: "Payroll template not found.", statusCode: StatusCodes.Status400BadRequest),
            PayrollRunOutcome.InvalidAmount => Problem(title: "Gross amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-PAY-3 — admin-triggered since no scheduler exists in this codebase.</summary>
    [HttpPost("run-recurring")]
    [Authorize(Policy = RunPolicy)]
    public async Task<IActionResult> RunRecurring(CancellationToken cancellationToken)
    {
        var count = await _payrollService.GenerateDueRecurringAsync(cancellationToken);
        return Ok(new { generated = count });
    }

    private static BCKash.Domain.Payroll.Payroll ToEntity(RunPayrollRequest request) => new()
    {
        PayrollTemplateId = request.PayrollTemplateId,
        GlAccountExpenseId = request.GlAccountExpenseId,
        GlAccountAssetId = request.GlAccountAssetId,
        UserId = request.UserId,
        OfficeId = request.OfficeId,
        EmployeeName = request.EmployeeName,
        BusinessName = request.BusinessName,
        PaymentMethod = request.PaymentMethod,
        PaymentTypeId = request.PaymentTypeId,
        BankName = request.BankName,
        AccountNumber = request.AccountNumber,
        Description = request.Description,
        Comments = request.Comments,
        GrossAmount = request.GrossAmount,
        Date = request.Date,
        Recurring = request.Recurring,
        RecurFrequency = request.RecurFrequency ?? "1",
        RecurStartDate = request.RecurStartDate,
        RecurEndDate = request.RecurEndDate,
        RecurType = request.RecurType,
    };

    private static PayrollResponse ToResponse(BCKash.Domain.Payroll.Payroll p) => new(
        p.Id, p.PayrollTemplateId, p.UserId, p.OfficeId, p.EmployeeName, p.BusinessName,
        p.GrossAmount, p.PaidAmount, p.Date, p.Recurring, p.RecurNextDate);
}
