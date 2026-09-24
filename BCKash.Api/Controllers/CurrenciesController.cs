using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/currencies")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public CurrenciesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CurrencyResponse>>> List(CancellationToken cancellationToken)
    {
        var currencies = await _db.Currencies.ToListAsync(cancellationToken);
        return Ok(currencies.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CurrencyResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var currency = await _db.Currencies.FindAsync([id], cancellationToken);
        return currency is null ? NotFound() : Ok(ToResponse(currency));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<CurrencyResponse>> Create(SaveCurrencyRequest request, CancellationToken cancellationToken)
    {
        var currency = new Currency
        {
            Name = request.Name,
            Code = request.Code,
            Symbol = request.Symbol,
            Decimals = request.Decimals,
            Xrate = request.Xrate,
            InternationalCode = request.InternationalCode,
            Active = request.Active,
        };
        _db.Currencies.Add(currency);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = currency.Id }, ToResponse(currency));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveCurrencyRequest request, CancellationToken cancellationToken)
    {
        var currency = await _db.Currencies.FindAsync([id], cancellationToken);
        if (currency is null)
        {
            return NotFound();
        }

        currency.Name = request.Name;
        currency.Code = request.Code;
        currency.Symbol = request.Symbol;
        currency.Decimals = request.Decimals;
        currency.Xrate = request.Xrate;
        currency.InternationalCode = request.InternationalCode;
        currency.Active = request.Active;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(currency));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var currency = await _db.Currencies.FindAsync([id], cancellationToken);
        if (currency is null)
        {
            return NotFound();
        }

        _db.Currencies.Remove(currency);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static CurrencyResponse ToResponse(Currency c) =>
        new(c.Id, c.Name, c.Code, c.Symbol, c.Decimals, c.Xrate, c.InternationalCode, c.Active);
}
