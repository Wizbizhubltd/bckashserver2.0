using BCKash.Api.Contracts;
using BCKash.Application.PayrollProcessing;
using BCKash.Domain.Payroll;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/payroll-templates")]
[Authorize]
public class PayrollTemplatesController : ControllerBase
{
    private const string ManagePolicy = "Permission:payroll.manage";

    private readonly BCKashDbContext _db;
    private readonly IPayrollTemplateService _templateService;

    public PayrollTemplatesController(BCKashDbContext db, IPayrollTemplateService templateService)
    {
        _db = db;
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollTemplateResponse>>> List(CancellationToken cancellationToken)
    {
        var templates = await _db.PayrollTemplates.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Ok(templates.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PayrollTemplateResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var template = await _db.PayrollTemplates.FindAsync([id], cancellationToken);
        return template is null ? NotFound() : Ok(ToResponse(template));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PayrollTemplateResponse>> Create(SavePayrollTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _templateService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Template!.Id }, ToResponse(result.Template));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SavePayrollTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _templateService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            PayrollTemplateWriteOutcome.Success => Ok(ToResponse(result.Template!)),
            PayrollTemplateWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _templateService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            PayrollTemplateWriteOutcome.Success => NoContent(),
            PayrollTemplateWriteOutcome.NotFound => NotFound(),
            PayrollTemplateWriteOutcome.InUse => Problem(title: "This template has payroll runs against it.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("{templateId:int}/line-items")]
    public async Task<ActionResult<IReadOnlyList<PayrollTemplateLineItemResponse>>> ListLineItems(int templateId, CancellationToken cancellationToken)
    {
        var lineItems = await _db.PayrollTemplateMeta.Where(m => m.PayrollTemplateId == templateId).OrderBy(m => m.Id).ToListAsync(cancellationToken);
        return Ok(lineItems.Select(ToLineItemResponse).ToList());
    }

    [HttpPost("{templateId:int}/line-items")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> AddLineItem(int templateId, SavePayrollTemplateLineItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _templateService.AddLineItemAsync(templateId, ToLineItemEntity(request), cancellationToken);
        return result.Outcome switch
        {
            PayrollTemplateWriteOutcome.Success => CreatedAtAction(
                nameof(ListLineItems), new { templateId }, ToLineItemResponse(result.LineItem!)),
            PayrollTemplateWriteOutcome.NotFound => Problem(title: "Payroll template not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{templateId:int}/line-items/{lineItemId:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> UpdateLineItem(int templateId, int lineItemId, SavePayrollTemplateLineItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _templateService.UpdateLineItemAsync(templateId, lineItemId, ToLineItemEntity(request), cancellationToken);
        return result.Outcome switch
        {
            PayrollTemplateWriteOutcome.Success => Ok(ToLineItemResponse(result.LineItem!)),
            PayrollTemplateWriteOutcome.LineItemNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{templateId:int}/line-items/{lineItemId:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RemoveLineItem(int templateId, int lineItemId, CancellationToken cancellationToken)
    {
        var result = await _templateService.RemoveLineItemAsync(templateId, lineItemId, cancellationToken);
        return result.Outcome switch
        {
            PayrollTemplateWriteOutcome.Success => NoContent(),
            PayrollTemplateWriteOutcome.LineItemNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static PayrollTemplate ToEntity(SavePayrollTemplateRequest request) => new()
    {
        Name = request.Name,
        Notes = request.Notes,
        Picture = request.Picture,
        Active = request.Active ?? true,
    };

    private static PayrollTemplateMeta ToLineItemEntity(SavePayrollTemplateLineItemRequest request) => new()
    {
        Name = request.Name,
        Position = request.Position,
        Type = request.Type,
        IsDefault = request.IsDefault,
        IsTax = request.IsTax,
        IsPercentage = request.IsPercentage,
        TaxOn = request.TaxOn,
        DefaultValue = request.DefaultValue,
    };

    private static PayrollTemplateResponse ToResponse(PayrollTemplate t) => new(t.Id, t.Name, t.Notes, t.Picture, t.Active);

    private static PayrollTemplateLineItemResponse ToLineItemResponse(PayrollTemplateMeta m) => new(
        m.Id, m.PayrollTemplateId, m.Name, m.Position, m.Type, m.IsDefault, m.IsTax, m.IsPercentage, m.TaxOn, m.DefaultValue);
}
