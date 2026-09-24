using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/payment-types")]
[Authorize]
public class PaymentTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public PaymentTypesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var paymentTypes = await _db.PaymentTypes.ToListAsync(cancellationToken);
        return Ok(paymentTypes.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var paymentType = await _db.PaymentTypes.FindAsync([id], cancellationToken);
        return paymentType is null ? NotFound() : Ok(ToResponse(paymentType));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PaymentTypeResponse>> Create(SavePaymentTypeRequest request, CancellationToken cancellationToken)
    {
        var paymentType = new PaymentType { Name = request.Name, Notes = request.Notes, IsCash = request.IsCash };
        _db.PaymentTypes.Add(paymentType);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = paymentType.Id }, ToResponse(paymentType));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SavePaymentTypeRequest request, CancellationToken cancellationToken)
    {
        var paymentType = await _db.PaymentTypes.FindAsync([id], cancellationToken);
        if (paymentType is null)
        {
            return NotFound();
        }

        paymentType.Name = request.Name;
        paymentType.Notes = request.Notes;
        paymentType.IsCash = request.IsCash;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(paymentType));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var paymentType = await _db.PaymentTypes.FindAsync([id], cancellationToken);
        if (paymentType is null)
        {
            return NotFound();
        }

        _db.PaymentTypes.Remove(paymentType);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---- Payment Details (nested — a payment type's non-cash detail templates, BR-ORG-4) ----

    [HttpGet("{paymentTypeId:int}/details")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentDetailResponse>>> ListDetails(int paymentTypeId, CancellationToken cancellationToken)
    {
        var details = await _db.PaymentDetails.Where(d => d.PaymentTypeId == paymentTypeId).ToListAsync(cancellationToken);
        return Ok(details.Select(ToDetailResponse).ToList());
    }

    [HttpPost("{paymentTypeId:int}/details")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<PaymentDetailResponse>> CreateDetail(int paymentTypeId, SavePaymentDetailRequest request, CancellationToken cancellationToken)
    {
        var detail = new PaymentDetail
        {
            PaymentTypeId = paymentTypeId,
            AccountNumber = request.AccountNumber,
            ChequeNumber = request.ChequeNumber,
            RoutingCode = request.RoutingCode,
            ReceiptNumber = request.ReceiptNumber,
            Bank = request.Bank,
            Notes = request.Notes,
        };
        _db.PaymentDetails.Add(detail);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ListDetails), new { paymentTypeId }, ToDetailResponse(detail));
    }

    [HttpDelete("{paymentTypeId:int}/details/{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DeleteDetail(int paymentTypeId, int id, CancellationToken cancellationToken)
    {
        var detail = await _db.PaymentDetails.FirstOrDefaultAsync(d => d.Id == id && d.PaymentTypeId == paymentTypeId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        _db.PaymentDetails.Remove(detail);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static PaymentTypeResponse ToResponse(PaymentType p) => new(p.Id, p.Name, p.Notes, p.IsCash);

    private static PaymentDetailResponse ToDetailResponse(PaymentDetail d) =>
        new(d.Id, d.PaymentTypeId, d.AccountNumber, d.ChequeNumber, d.RoutingCode, d.ReceiptNumber, d.Bank, d.Notes);
}
