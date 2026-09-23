using BCKash.Api.Contracts;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/gl/closures")]
[Authorize]
public class GlClosuresController : ControllerBase
{
    private const string ManagePolicy = "Permission:gl.manage";

    // FR-GL-4: "reopening requires elevated permission" — a separate slug from ordinary closing,
    // same two-slug split as loan-applications.manage/.approve.
    private const string ReopenPolicy = "Permission:gl.closure-reopen";

    private readonly BCKashDbContext _db;
    private readonly IGlClosureService _closureService;

    public GlClosuresController(BCKashDbContext db, IGlClosureService closureService)
    {
        _db = db;
        _closureService = closureService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GlClosureResponse>>> List([FromQuery] int? officeId, CancellationToken cancellationToken)
    {
        var query = _db.GlClosures.AsQueryable();
        if (officeId.HasValue)
        {
            query = query.Where(c => c.OfficeId == officeId);
        }

        var closures = await query.OrderByDescending(c => c.ClosingDate).ToListAsync(cancellationToken);
        return Ok(closures.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(CreateGlClosureRequest request, CancellationToken cancellationToken)
    {
        var result = await _closureService.CloseAsync(request.OfficeId, request.ClosingDate, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            GlClosureWriteOutcome.Success => Ok(ToResponse(result.Closure!)),
            GlClosureWriteOutcome.AlreadyClosed => Problem(
                title: "This office already has an active closure at or after the requested date.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/reopen")]
    [Authorize(Policy = ReopenPolicy)]
    public async Task<IActionResult> Reopen(int id, ReopenGlClosureRequest request, CancellationToken cancellationToken)
    {
        var result = await _closureService.ReopenAsync(id, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            GlClosureWriteOutcome.Success => Ok(ToResponse(result.Closure!)),
            GlClosureWriteOutcome.NotFound => NotFound(),
            GlClosureWriteOutcome.AlreadyReopened => Problem(title: "This closure has already been reopened.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static GlClosureResponse ToResponse(GlClosure c) => new(c.Id, c.OfficeId, c.ClosingDate, c.Notes, c.ReopenedAt, c.ReopenedById);
}
