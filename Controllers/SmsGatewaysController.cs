using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/sms-gateways")]
[Authorize]
public class SmsGatewaysController : ControllerBase
{
    private const string ManagePolicy = "Permission:campaigns.manage";

    private readonly BCKashDbContext _db;
    private readonly ISmsGatewayService _gatewayService;

    public SmsGatewaysController(BCKashDbContext db, ISmsGatewayService gatewayService)
    {
        _db = db;
        _gatewayService = gatewayService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmsGatewayResponse>>> List(CancellationToken cancellationToken)
    {
        var gateways = await _db.SmsGateways.OrderBy(g => g.Name).ToListAsync(cancellationToken);
        return Ok(gateways.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SmsGatewayResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var gateway = await _db.SmsGateways.FindAsync([id], cancellationToken);
        return gateway is null ? NotFound() : Ok(ToResponse(gateway));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SmsGatewayResponse>> Create(SaveSmsGatewayRequest request, CancellationToken cancellationToken)
    {
        var result = await _gatewayService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Gateway!.Id }, ToResponse(result.Gateway));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveSmsGatewayRequest request, CancellationToken cancellationToken)
    {
        var result = await _gatewayService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            SmsGatewayWriteOutcome.Success => Ok(ToResponse(result.Gateway!)),
            SmsGatewayWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _gatewayService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            SmsGatewayWriteOutcome.Success => NoContent(),
            SmsGatewayWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static SmsGateway ToEntity(SaveSmsGatewayRequest request) => new()
    {
        Name = request.Name,
        FromName = request.FromName,
        ToName = request.ToName,
        Url = request.Url,
        MsgName = request.MsgName,
        Notes = request.Notes,
    };

    private static SmsGatewayResponse ToResponse(SmsGateway g) => new(g.Id, g.Name, g.FromName, g.ToName, g.Url, g.MsgName, g.Notes);
}
