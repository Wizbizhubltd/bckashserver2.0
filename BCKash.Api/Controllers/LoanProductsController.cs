using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/loan-products")]
[Authorize]
public class LoanProductsController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-products.manage";

    private readonly BCKashDbContext _db;
    private readonly ILoanProductService _loanProductService;

    public LoanProductsController(BCKashDbContext db, ILoanProductService loanProductService)
    {
        _db = db;
        _loanProductService = loanProductService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanProductResponse>>> List(CancellationToken cancellationToken)
    {
        var products = await _db.LoanProducts.OrderByDescending(p => p.Id).ToListAsync(cancellationToken);
        return Ok(products.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanProductResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var product = await _db.LoanProducts.FindAsync([id], cancellationToken);
        return product is null ? NotFound() : Ok(ToResponse(product));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<LoanProductResponse>> Create(SaveLoanProductRequest request, CancellationToken cancellationToken)
    {
        var product = ToEntity(request);
        var result = await _loanProductService.CreateAsync(product, cancellationToken);

        return result.Outcome switch
        {
            LoanProductWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Product!.Id }, ToResponse(result.Product)),
            LoanProductWriteOutcome.InvalidRange => Problem(
                title: "Minimum must be ≤ default, and default must be ≤ maximum, for principal, term, and interest rate.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanProductWriteOutcome.Invalid => Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest),
            LoanProductWriteOutcome.DuplicateName => Problem(title: "Another loan product already has this name.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveLoanProductRequest request, CancellationToken cancellationToken)
    {
        var updated = ToEntity(request);
        var result = await _loanProductService.UpdateAsync(id, updated, cancellationToken);

        return result.Outcome switch
        {
            LoanProductWriteOutcome.Success => Ok(ToResponse(result.Product!)),
            LoanProductWriteOutcome.NotFound => NotFound(),
            LoanProductWriteOutcome.InvalidRange => Problem(
                title: "Minimum must be ≤ default, and default must be ≤ maximum, for principal, term, and interest rate.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanProductWriteOutcome.Invalid => Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest),
            LoanProductWriteOutcome.DuplicateName => Problem(title: "Another loan product already has this name.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-LN-1: a product with any Loan/LoanApplication against it cannot be deleted — deactivate instead.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _loanProductService.DeleteAsync(id, cancellationToken);

        return result.Outcome switch
        {
            LoanProductWriteOutcome.Success => NoContent(),
            LoanProductWriteOutcome.NotFound => NotFound(),
            LoanProductWriteOutcome.InUse => Problem(
                title: "This product has loans or applications against it — deactivate it instead of deleting.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>The fees and penalties attached to the product — attached penalties are the only ones the daily penalty run charges on its loans.</summary>
    [HttpGet("{id:int}/charges")]
    public async Task<ActionResult<IReadOnlyCollection<int>>> Charges(int id, CancellationToken cancellationToken)
    {
        if (!await _db.LoanProducts.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _db.LoanProductCharges
            .Where(pc => pc.LoanProductId == id && pc.ChargeId.HasValue)
            .Select(pc => pc.ChargeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken));
    }

    /// <summary>Replaces the product's attached fees and penalties. Only active loan-level charges can be attached.</summary>
    [HttpPut("{id:int}/charges")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyCollection<int>>> SetCharges(int id, SetLoanProductChargesRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.LoanProducts.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var wanted = request.ChargeIds.Distinct().ToList();
        var valid = await _db.Charges.Where(c => wanted.Contains(c.Id) && c.Product == ChargeProduct.Loan && c.Active).Select(c => c.Id).ToListAsync(cancellationToken);
        if (valid.Count != wanted.Count)
        {
            return Problem(title: "Only active loan fees and penalties can be attached to a loan product.", statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await _db.LoanProductCharges.Where(pc => pc.LoanProductId == id).ToListAsync(cancellationToken);
        _db.LoanProductCharges.RemoveRange(existing.Where(pc => pc.ChargeId is not { } chargeId || !wanted.Contains(chargeId)));
        var kept = existing.Where(pc => pc.ChargeId.HasValue).Select(pc => pc.ChargeId!.Value).ToHashSet();
        foreach (var chargeId in wanted.Where(c => !kept.Contains(c)))
        {
            _db.LoanProductCharges.Add(new LoanProductCharge { LoanProductId = id, ChargeId = chargeId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(wanted);
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var result = await _loanProductService.ActivateAsync(id, cancellationToken);
        return result.Outcome == LoanProductWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Product!));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _loanProductService.DeactivateAsync(id, cancellationToken);
        return result.Outcome == LoanProductWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Product!));
    }

    private static LoanProduct ToEntity(SaveLoanProductRequest r) => new()
    {
        Name = r.Name,
        ShortName = r.ShortName,
        Description = r.Description,
        FundId = r.FundId,
        CurrencyId = r.CurrencyId,
        Decimals = r.Decimals,
        MinimumPrincipal = r.MinimumPrincipal,
        DefaultPrincipal = r.DefaultPrincipal,
        MaximumPrincipal = r.MaximumPrincipal,
        MinimumLoanTerm = r.MinimumLoanTerm,
        DefaultLoanTerm = r.DefaultLoanTerm,
        MaximumLoanTerm = r.MaximumLoanTerm,
        RepaymentFrequency = r.RepaymentFrequency,
        RepaymentFrequencyType = r.RepaymentFrequencyType,
        MinimumInterestRate = r.MinimumInterestRate,
        DefaultInterestRate = r.DefaultInterestRate,
        MaximumInterestRate = r.MaximumInterestRate,
        InterestRateType = r.InterestRateType,
        GraceOnInterestCharged = r.GraceOnInterestCharged,
        GraceOnPrincipal = r.GraceOnPrincipal,
        GraceOnInterestPayment = r.GraceOnInterestPayment,
        AllowCustomGrace = r.AllowCustomGrace,
        AllowStandingInstructions = r.AllowStandingInstructions,
        InterestMethod = r.InterestMethod,
        AmortizationMethod = r.AmortizationMethod,
        InterestCalculationPeriodType = r.InterestCalculationPeriodType,
        YearDays = r.YearDays,
        MonthDays = r.MonthDays,
        LoanTransactionStrategy = r.LoanTransactionStrategy,
        IncludeInCycle = r.IncludeInCycle,
        LockGuarantee = r.LockGuarantee,
        AllocateOverpayments = r.AllocateOverpayments,
        AllowAdditionalCharges = r.AllowAdditionalCharges,
        AccountingRule = r.AccountingRule,
        NpaDays = r.NpaDays,
        ArrearsGraceDays = r.ArrearsGraceDays,
        NpaSuspendIncome = r.NpaSuspendIncome,
        GlAccountFundSourceId = r.GlAccountFundSourceId,
        GlAccountLoanPortfolioId = r.GlAccountLoanPortfolioId,
        GlAccountReceivableInterestId = r.GlAccountReceivableInterestId,
        GlAccountReceivableFeeId = r.GlAccountReceivableFeeId,
        GlAccountReceivablePenaltyId = r.GlAccountReceivablePenaltyId,
        GlAccountLoanOverPaymentsId = r.GlAccountLoanOverPaymentsId,
        GlAccountSuspendedIncomeId = r.GlAccountSuspendedIncomeId,
        GlAccountIncomeInterestId = r.GlAccountIncomeInterestId,
        GlAccountIncomeFeeId = r.GlAccountIncomeFeeId,
        GlAccountIncomePenaltyId = r.GlAccountIncomePenaltyId,
        GlAccountIncomeRecoveryId = r.GlAccountIncomeRecoveryId,
        GlAccountLoansWrittenOffId = r.GlAccountLoansWrittenOffId,
    };

    private static LoanProductResponse ToResponse(LoanProduct p) => new(
        p.Id, p.Name, p.ShortName, p.Description, p.FundId, p.CurrencyId, p.Decimals,
        p.MinimumPrincipal, p.DefaultPrincipal, p.MaximumPrincipal,
        p.MinimumLoanTerm, p.DefaultLoanTerm, p.MaximumLoanTerm,
        p.RepaymentFrequency, p.RepaymentFrequencyType,
        p.MinimumInterestRate, p.DefaultInterestRate, p.MaximumInterestRate, p.InterestRateType,
        p.GraceOnInterestCharged, p.GraceOnPrincipal, p.GraceOnInterestPayment, p.AllowCustomGrace, p.AllowStandingInstructions,
        p.InterestMethod, p.AmortizationMethod,
        p.InterestCalculationPeriodType, p.YearDays, p.MonthDays,
        p.LoanTransactionStrategy, p.IncludeInCycle, p.LockGuarantee, p.AllocateOverpayments, p.AllowAdditionalCharges,
        p.AccountingRule, p.NpaDays, p.ArrearsGraceDays, p.NpaSuspendIncome,
        p.GlAccountFundSourceId, p.GlAccountLoanPortfolioId, p.GlAccountReceivableInterestId, p.GlAccountReceivableFeeId,
        p.GlAccountReceivablePenaltyId, p.GlAccountLoanOverPaymentsId, p.GlAccountSuspendedIncomeId, p.GlAccountIncomeInterestId,
        p.GlAccountIncomeFeeId, p.GlAccountIncomePenaltyId, p.GlAccountIncomeRecoveryId, p.GlAccountLoansWrittenOffId,
        p.Active);
}
