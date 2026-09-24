using BCKash.Api.Contracts;
using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/other-income")]
[Authorize]
public class OtherIncomeController : ControllerBase
{
    private const string ManagePolicy = "Permission:other-income.manage";
    private const string ApprovePolicy = "Permission:other-income.approve";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IOtherIncomeService _incomeService;

    public OtherIncomeController(BCKashDbContext db, IOtherIncomeService incomeService)
    {
        _db = db;
        _incomeService = incomeService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OtherIncomeResponse>>> List(
        [FromQuery] ApprovalStatus? status,
        [FromQuery] int? officeId,
        [FromQuery] int? otherIncomeTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.OtherIncomes.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status);
        }

        if (officeId.HasValue)
        {
            query = query.Where(o => o.OfficeId == officeId);
        }

        if (otherIncomeTypeId.HasValue)
        {
            query = query.Where(o => o.OtherIncomeTypeId == otherIncomeTypeId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var incomes = await query.OrderByDescending(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<OtherIncomeResponse>(incomes.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OtherIncomeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var income = await _db.OtherIncomes.FindAsync([id], cancellationToken);
        return income is null ? NotFound() : Ok(ToResponse(income));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveOtherIncomeRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeService.CreateAsync(ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            OtherIncomeWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.OtherIncome!.Id }, ToResponse(result.OtherIncome)),
            OtherIncomeWriteOutcome.TypeNotFound => Problem(title: "Other income type not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveOtherIncomeRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return ToResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _incomeService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            OtherIncomeWriteOutcome.Success => NoContent(),
            OtherIncomeWriteOutcome.NotFound => NotFound(),
            OtherIncomeWriteOutcome.InvalidTransition => Problem(title: "Only a pending record can be deleted.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Approve(int id, ApproveRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeService.ApproveAsync(id, request.Notes, cancellationToken);
        return ToResult(result);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToResult(result);
    }

    private IActionResult ToResult(OtherIncomeWriteResult result) => result.Outcome switch
    {
        OtherIncomeWriteOutcome.Success => Ok(ToResponse(result.OtherIncome!)),
        OtherIncomeWriteOutcome.NotFound => NotFound(),
        OtherIncomeWriteOutcome.TypeNotFound => Problem(title: "Other income type not found.", statusCode: StatusCodes.Status400BadRequest),
        OtherIncomeWriteOutcome.InvalidTransition => Problem(title: "This record is no longer pending.", statusCode: StatusCodes.Status400BadRequest),
        OtherIncomeWriteOutcome.ReasonRequired => Problem(title: "A reason is required to decline.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static OtherIncome ToEntity(SaveOtherIncomeRequest request) => new()
    {
        OfficeId = request.OfficeId,
        OtherIncomeTypeId = request.OtherIncomeTypeId,
        Name = request.Name,
        Amount = request.Amount,
        Date = request.Date,
        Year = request.Date?.Year.ToString(),
        Month = request.Date?.Month.ToString(),
        Notes = request.Notes,
        Files = request.Files,
    };

    private static OtherIncomeResponse ToResponse(OtherIncome o) => new(
        o.Id, o.OfficeId, o.OtherIncomeTypeId, o.Name, o.Amount, o.Date, o.Status,
        o.ApprovedById, o.ApprovedDate, o.DeclinedById, o.DeclinedDate, o.Notes);
}
