using BCKash.Application.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

/// <summary>
/// FR-SAV-4's interest accrual/posting "job". No scheduler exists anywhere in this codebase
/// (same situation as Phase 5's NPA recompute) — this admin-triggered endpoint is the stand-in,
/// safe to call repeatedly since ISavingsInterestPostingService only acts on accounts whose
/// next calculation/posting date is actually due.
/// </summary>
[ApiController]
[Route("api/savings-interest")]
[Authorize]
public class SavingsInterestController : ControllerBase
{
    private readonly ISavingsInterestPostingService _interestPostingService;

    public SavingsInterestController(ISavingsInterestPostingService interestPostingService)
    {
        _interestPostingService = interestPostingService;
    }

    [HttpPost("run")]
    [Authorize(Policy = "Permission:savings-accounts.manage")]
    public async Task<IActionResult> Run([FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken)
    {
        await _interestPostingService.RunDueAsync(asOfDate, cancellationToken);
        return Ok();
    }
}
