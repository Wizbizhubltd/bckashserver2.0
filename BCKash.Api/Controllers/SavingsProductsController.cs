using BCKash.Api.Contracts;
using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/savings-products")]
[Authorize]
public class SavingsProductsController : ControllerBase
{
    private const string ManagePolicy = "Permission:savings-products.manage";

    private readonly BCKashDbContext _db;
    private readonly ISavingsProductService _savingsProductService;

    public SavingsProductsController(BCKashDbContext db, ISavingsProductService savingsProductService)
    {
        _db = db;
        _savingsProductService = savingsProductService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavingsProductResponse>>> List(CancellationToken cancellationToken)
    {
        var products = await _db.SavingsProducts.ToListAsync(cancellationToken);
        return Ok(products.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SavingsProductResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var product = await _db.SavingsProducts.FindAsync([id], cancellationToken);
        return product is null ? NotFound() : Ok(ToResponse(product));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SavingsProductResponse>> Create(SaveSavingsProductRequest request, CancellationToken cancellationToken)
    {
        var product = ToEntity(request);
        var result = await _savingsProductService.CreateAsync(product, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Product!.Id }, ToResponse(result.Product));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveSavingsProductRequest request, CancellationToken cancellationToken)
    {
        var updated = ToEntity(request);
        var result = await _savingsProductService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome == SavingsProductWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Product!));
    }

    /// <summary>FR-SAV-1: a product with any SavingsAccount against it cannot be deleted — deactivate instead.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _savingsProductService.DeleteAsync(id, cancellationToken);

        return result.Outcome switch
        {
            SavingsProductWriteOutcome.Success => NoContent(),
            SavingsProductWriteOutcome.NotFound => NotFound(),
            SavingsProductWriteOutcome.InUse => Problem(
                title: "This product has savings accounts against it — deactivate it instead of deleting.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var result = await _savingsProductService.ActivateAsync(id, cancellationToken);
        return result.Outcome == SavingsProductWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Product!));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _savingsProductService.DeactivateAsync(id, cancellationToken);
        return result.Outcome == SavingsProductWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Product!));
    }

    private static SavingsProduct ToEntity(SaveSavingsProductRequest r) => new()
    {
        Name = r.Name,
        ShortName = r.ShortName,
        Description = r.Description,
        CurrencyId = r.CurrencyId,
        Decimals = r.Decimals,
        InterestRate = r.InterestRate,
        AllowOverdraft = r.AllowOverdraft,
        MinimumBalance = r.MinimumBalance,
        InterestCompoundingPeriod = r.InterestCompoundingPeriod,
        InterestPostingPeriod = r.InterestPostingPeriod,
        InterestCalculationType = r.InterestCalculationType,
        AllowTransferWithdrawalFee = r.AllowTransferWithdrawalFee,
        OpeningBalance = r.OpeningBalance,
        AllowAdditionalCharges = r.AllowAdditionalCharges,
        YearDays = r.YearDays,
        AccountingRule = r.AccountingRule,
        GlAccountSavingsReferenceId = r.GlAccountSavingsReferenceId,
        GlAccountOverdraftPortfolioId = r.GlAccountOverdraftPortfolioId,
        GlAccountSavingsControlId = r.GlAccountSavingsControlId,
        GlAccountInterestOnSavingsId = r.GlAccountInterestOnSavingsId,
        GlAccountSavingsWrittenOffId = r.GlAccountSavingsWrittenOffId,
        GlAccountIncomeInterestId = r.GlAccountIncomeInterestId,
        GlAccountIncomeFeeId = r.GlAccountIncomeFeeId,
        GlAccountIncomePenaltyId = r.GlAccountIncomePenaltyId,
    };

    private static SavingsProductResponse ToResponse(SavingsProduct p) => new(
        p.Id, p.Name, p.ShortName, p.Description, p.CurrencyId, p.Decimals,
        p.InterestRate, p.AllowOverdraft, p.MinimumBalance,
        p.InterestCompoundingPeriod, p.InterestPostingPeriod, p.InterestCalculationType,
        p.AllowTransferWithdrawalFee, p.OpeningBalance, p.AllowAdditionalCharges, p.YearDays, p.AccountingRule,
        p.GlAccountSavingsReferenceId, p.GlAccountOverdraftPortfolioId, p.GlAccountSavingsControlId,
        p.GlAccountInterestOnSavingsId, p.GlAccountSavingsWrittenOffId, p.GlAccountIncomeInterestId,
        p.GlAccountIncomeFeeId, p.GlAccountIncomePenaltyId, p.Active);
}
