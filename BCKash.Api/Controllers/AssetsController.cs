using BCKash.Api.Contracts;
using BCKash.Application.Assets;
using BCKash.Domain.Assets;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private const string ManagePolicy = "Permission:assets.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IAssetService _assetService;
    private readonly IAssetDepreciationService _depreciationService;

    public AssetsController(BCKashDbContext db, IAssetService assetService, IAssetDepreciationService depreciationService)
    {
        _db = db;
        _assetService = assetService;
        _depreciationService = depreciationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetResponse>>> List(
        [FromQuery] int? officeId,
        [FromQuery] int? assetTypeId,
        [FromQuery] AssetStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Assets.AsQueryable();
        if (officeId.HasValue)
        {
            query = query.Where(a => a.OfficeId == officeId);
        }

        if (assetTypeId.HasValue)
        {
            query = query.Where(a => a.AssetTypeId == assetTypeId);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var assets = await query.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<AssetResponse>(assets.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var asset = await _db.Assets.FindAsync([id], cancellationToken);
        return asset is null ? NotFound() : Ok(ToResponse(asset));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveAssetRequest request, CancellationToken cancellationToken)
    {
        var result = await _assetService.CreateAsync(ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            AssetWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Asset!.Id }, ToResponse(result.Asset)),
            AssetWriteOutcome.TypeNotFound => Problem(title: "Asset type not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveAssetRequest request, CancellationToken cancellationToken)
    {
        var result = await _assetService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            AssetWriteOutcome.Success => Ok(ToResponse(result.Asset!)),
            AssetWriteOutcome.NotFound => NotFound(),
            AssetWriteOutcome.TypeNotFound => Problem(title: "Asset type not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _assetService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            AssetWriteOutcome.Success => NoContent(),
            AssetWriteOutcome.NotFound => NotFound(),
            AssetWriteOutcome.InUse => Problem(title: "This asset already has depreciation history.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/status")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeAssetStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _assetService.ChangeStatusAsync(id, request.Status, cancellationToken);
        return result.Outcome switch
        {
            AssetWriteOutcome.Success => Ok(ToResponse(result.Asset!)),
            AssetWriteOutcome.NotFound => NotFound(),
            AssetWriteOutcome.InvalidTransition => Problem(
                title: "This asset has already reached a terminal status (sold/written off) and can no longer change.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("{id:int}/depreciation-schedule")]
    public async Task<ActionResult<IReadOnlyList<AssetDepreciationResponse>>> DepreciationSchedule(int id, CancellationToken cancellationToken)
    {
        var rows = await _db.AssetDepreciations.Where(d => d.AssetId == id).OrderBy(d => d.Id).ToListAsync(cancellationToken);
        return Ok(rows.Select(ToDepreciationResponse).ToList());
    }

    /// <summary>FR-AST-2 — runs the next due annual depreciation year for one asset.</summary>
    [HttpPost("{id:int}/depreciate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Depreciate(int id, RunDepreciationRequest request, CancellationToken cancellationToken)
    {
        var result = await _depreciationService.RunAsync(id, request.Year, cancellationToken);
        return result.Outcome switch
        {
            AssetDepreciationOutcome.Success => Ok(ToDepreciationResponse(result.Depreciation!)),
            AssetDepreciationOutcome.NotFound => NotFound(),
            AssetDepreciationOutcome.NotActive => Problem(title: "Only active assets can be depreciated.", statusCode: StatusCodes.Status400BadRequest),
            AssetDepreciationOutcome.AlreadyRun => Problem(title: "This asset already has a depreciation row for that year.", statusCode: StatusCodes.Status409Conflict),
            AssetDepreciationOutcome.FullyDepreciated => Problem(title: "This asset has already reached its salvage value.", statusCode: StatusCodes.Status409Conflict),
            AssetDepreciationOutcome.MissingConfiguration => Problem(
                title: "Cost, salvage value or useful life aren't set (or cost isn't greater than salvage value).",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Admin-triggered bulk run for every active asset not yet depreciated for the given year —
    /// no scheduler exists in this codebase (FR-AST-2). Returns how many assets were depreciated.
    /// </summary>
    [HttpPost("depreciation/run-due")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RunDueDepreciation(RunDepreciationRequest request, CancellationToken cancellationToken)
    {
        var count = await _depreciationService.RunDueAsync(request.Year, cancellationToken);
        return Ok(new { depreciated = count });
    }

    private static Asset ToEntity(SaveAssetRequest request) => new()
    {
        AssetTypeId = request.AssetTypeId,
        OfficeId = request.OfficeId,
        Name = request.Name,
        PurchaseDate = request.PurchaseDate,
        PurchasePrice = request.PurchasePrice,
        LifeSpan = request.LifeSpan,
        SalvageValue = request.SalvageValue,
        SerialNumber = request.SerialNumber,
        Notes = request.Notes,
        Files = request.Files,
        PurchaseYear = request.PurchaseYear,
    };

    private static AssetResponse ToResponse(Asset a) => new(
        a.Id, a.AssetTypeId, a.OfficeId, a.Name, a.PurchaseDate, a.PurchasePrice, a.Value,
        a.LifeSpan, a.SalvageValue, a.SerialNumber, a.Notes, a.Files, a.PurchaseYear, a.Status);

    private static AssetDepreciationResponse ToDepreciationResponse(AssetDepreciation d) => new(
        d.Id, d.AssetId, d.Year, d.BeginningValue, d.DepreciationValue, d.Rate, d.Cost, d.Accumulated, d.EndingValue);
}
