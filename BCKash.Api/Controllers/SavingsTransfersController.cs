using BCKash.Api.Contracts;
using BCKash.Application.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

/// <summary>FR-SAV-6's linked loan↔savings transfers.</summary>
[ApiController]
[Route("api/v1/savings-transfers")]
[Authorize]
public class SavingsTransfersController : ControllerBase
{
    private const string ManagePolicy = "Permission:savings-accounts.manage";

    private readonly ISavingsTransferService _transferService;

    public SavingsTransfersController(ISavingsTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost("repay-loan")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RepayLoan(RepayLoanFromSavingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _transferService.RepayLoanFromSavingsAsync(request.SavingsId, request.LoanId, request.Amount, request.Date, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsTransferOutcome.Success => Ok(new { overpayment = result.Overpayment }),
            SavingsTransferOutcome.SavingsNotFound => NotFound("Savings account not found."),
            SavingsTransferOutcome.LoanNotFound => NotFound("Loan not found."),
            SavingsTransferOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransferOutcome.SavingsAccountNotTransactable => Problem(title: "Only an approved savings account can fund a transfer.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransferOutcome.InvalidLoanStatus => Problem(title: "This loan has no active schedule to repay against.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransferOutcome.InsufficientBalance => Problem(
                title: "This transfer would take the savings balance below its minimum balance (or overdraft limit).",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("disburse-to-savings")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DisburseToSavings(DisburseLoanToSavingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _transferService.DisburseLoanToSavingsAsync(request.LoanId, request.SavingsId, request.Amount, request.Date, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsTransferOutcome.Success => Ok(),
            SavingsTransferOutcome.SavingsNotFound => NotFound("Savings account not found."),
            SavingsTransferOutcome.LoanNotFound => NotFound("Loan not found."),
            SavingsTransferOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransferOutcome.SavingsAccountNotTransactable => Problem(title: "Only an approved savings account can receive a transfer.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
