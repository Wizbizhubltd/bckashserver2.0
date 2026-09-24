using BCKash.Api.Contracts;
using BCKash.Application.Assets;
using BCKash.Domain.Assets;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/asset-types")]
[Authorize]
public class AssetTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:assets.manage";

    private readonly BCKashDbContext _db;
    private readonly IAssetTypeService _assetTypeService;

    public AssetTypesController(BCKashDbContext db, IAssetTypeService assetTypeService)
    {
        _db = db;
        _assetTypeService = assetTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssetTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var types = await _db.AssetTypes.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Ok(types.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var type = await _db.AssetTypes.FindAsync([id], cancellationToken);
        return type is null ? NotFound() : Ok(ToResponse(type));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<AssetTypeResponse>> Create(SaveAssetTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _assetTypeService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.AssetType!.Id }, ToResponse(result.AssetType));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveAssetTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _assetTypeService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            AssetTypeWriteOutcome.Success => Ok(ToResponse(result.AssetType!)),
            AssetTypeWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _assetTypeService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            AssetTypeWriteOutcome.Success => NoContent(),
            AssetTypeWriteOutcome.NotFound => NotFound(),
            AssetTypeWriteOutcome.InUse => Problem(title: "This asset type has assets registered against it.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static AssetType ToEntity(SaveAssetTypeRequest request) => new()
    {
        Name = request.Name,
        GlAccountFixedAssetId = request.GlAccountFixedAssetId,
        GlAccountAssetId = request.GlAccountAssetId,
        GlAccountContraAssetId = request.GlAccountContraAssetId,
        GlAccountExpenseId = request.GlAccountExpenseId,
        GlAccountLiabilityId = request.GlAccountLiabilityId,
        GlAccountIncomeId = request.GlAccountIncomeId,
        Notes = request.Notes,
    };

    private static AssetTypeResponse ToResponse(AssetType t) => new(
        t.Id, t.Name, t.GlAccountFixedAssetId, t.GlAccountAssetId, t.GlAccountContraAssetId,
        t.GlAccountExpenseId, t.GlAccountLiabilityId, t.GlAccountIncomeId, t.Notes);
}
