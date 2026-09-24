using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Guarantors captured at the application stage (FR-LN-21). Ownership-scoped CRUD, no service —
/// same "no state machine to guard" reasoning as the Phase 2/3 sub-resource controllers.
/// </summary>
[ApiController]
[Route("api/v1/loan-applications/{applicationId:int}/guarantors")]
[Authorize]
public class LoanApplicationGuarantorsController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-applications.manage";

    private readonly BCKashDbContext _db;

    public LoanApplicationGuarantorsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GuarantorResponse>>> List(int applicationId, CancellationToken cancellationToken)
    {
        if (!await _db.LoanApplications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.Guarantors.Where(g => g.LoanApplicationId == applicationId).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GuarantorResponse>> Get(int applicationId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.Guarantors.FirstOrDefaultAsync(g => g.Id == id && g.LoanApplicationId == applicationId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<GuarantorResponse>> Create(int applicationId, SaveGuarantorRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.LoanApplications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            return NotFound();
        }

        var item = ToEntity(request);
        item.LoanApplicationId = applicationId;
        _db.Guarantors.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { applicationId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int applicationId, int id, SaveGuarantorRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.Guarantors.FirstOrDefaultAsync(g => g.Id == id && g.LoanApplicationId == applicationId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var updated = ToEntity(request);
        item.ClientId = updated.ClientId;
        item.IsClient = updated.IsClient;
        item.ClientRelationshipId = updated.ClientRelationshipId;
        item.Amount = updated.Amount;
        item.Title = updated.Title;
        item.FirstName = updated.FirstName;
        item.MiddleName = updated.MiddleName;
        item.LastName = updated.LastName;
        item.Gender = updated.Gender;
        item.Dob = updated.Dob;
        item.Street = updated.Street;
        item.Address = updated.Address;
        item.Mobile = updated.Mobile;
        item.Phone = updated.Phone;
        item.Email = updated.Email;
        item.Work = updated.Work;
        item.WorkAddress = updated.WorkAddress;
        item.LockFunds = updated.LockFunds;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int applicationId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.Guarantors.FirstOrDefaultAsync(g => g.Id == id && g.LoanApplicationId == applicationId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.Guarantors.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static Guarantor ToEntity(SaveGuarantorRequest r) => new()
    {
        ClientId = r.ClientId,
        IsClient = r.IsClient,
        ClientRelationshipId = r.ClientRelationshipId,
        Amount = r.Amount,
        Title = r.Title,
        FirstName = r.FirstName,
        MiddleName = r.MiddleName,
        LastName = r.LastName,
        Gender = r.Gender,
        Dob = r.Dob,
        Street = r.Street,
        Address = r.Address,
        Mobile = r.Mobile,
        Phone = r.Phone,
        Email = r.Email,
        Work = r.Work,
        WorkAddress = r.WorkAddress,
        LockFunds = r.LockFunds,
    };

    private static GuarantorResponse ToResponse(Guarantor g) => new(
        g.Id, g.ClientId, g.LoanId, g.LoanApplicationId, g.IsClient, g.ClientRelationshipId, g.Amount,
        g.Title, g.FirstName, g.MiddleName, g.LastName, g.Gender, g.Dob,
        g.Street, g.Address, g.Mobile, g.Phone, g.Email, g.Work, g.WorkAddress, g.LockFunds);
}
