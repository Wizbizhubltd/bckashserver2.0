using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

public class LoanApplicationService : ILoanApplicationService
{
    // Same reasoning as ClientService's account-number generation: the unique index on
    // Loan.AccountNumber is the real concurrency safety net, this just bounds retries.
    private const int MaxAccountNumberRetries = 5;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILoanApplicationClientCodeService _clientCodes;
    private readonly IOfficeFundService _officeFunds;

    public LoanApplicationService(BCKashDbContext db, ICurrentUserContext currentUser, ILoanApplicationClientCodeService clientCodes, IOfficeFundService officeFunds)
    {
        _officeFunds = officeFunds;
        _db = db;
        _currentUser = currentUser;
        _clientCodes = clientCodes;
    }

    public async Task<LoanApplicationWriteResult> CreateAsync(LoanApplication application, ClientCodeSubmission? clientCode = null, CancellationToken cancellationToken = default)
    {
        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == application.LoanProductId, cancellationToken);
        if (product is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ProductNotFound);
        }

        var rangeCheck = CheckAmountAndTerm(application.Amount, application.LoanTerm, product);
        if (rangeCheck is not null)
        {
            return new LoanApplicationWriteResult(rangeCheck.Value);
        }

        var check = await _clientCodes.CheckAsync(application, clientCode, cancellationToken);
        switch (check.Outcome)
        {
            case ClientCodeCheckOutcome.Missing:
                return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientCodeRequired);
            case ClientCodeCheckOutcome.Mismatch:
                return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientCodeMismatch);
            case ClientCodeCheckOutcome.Expired:
                return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientCodeExpired);
            case ClientCodeCheckOutcome.Incorrect:
                return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientCodeIncorrect, ClientCodeAttemptsLeft: check.AttemptsLeft);
        }

        application.UserId = _currentUser.UserId;
        application.Status = ApprovalStatus.Pending;

        // Every applicant pays the application form fee in force today.
        if (await ApplicationFormFee.CurrentAsync(_db, cancellationToken) is { } formFee)
        {
            application.FormFee = formFee.Amount;
            application.FormFeeChargeId = formFee.Id;
        }

        _db.LoanApplications.Add(application);

        // The code is used up in the same save as the application: both happen, or neither does.
        if (check.Code is not null)
        {
            check.Code.ConsumedAtUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (check.Code is not null)
        {
            // Someone submitted the same code a moment earlier.
            _db.ChangeTracker.Clear();
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientCodeExpired);
        }

        if (check.Code is not null)
        {
            check.Code.LoanApplicationId = application.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.Success, application);
    }

    public async Task<LoanApplicationWriteResult> UpdateAsync(int id, LoanApplication updated, CancellationToken cancellationToken = default)
    {
        var application = await _db.LoanApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.NotFound);
        }

        if (application.Status != ApprovalStatus.Pending)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.InvalidTransition);
        }

        // The client agreed to a specific loan by code — otherwise staff could raise a small loan
        // with the client's confirmation and then edit it into a bigger one.
        var termsChanged = updated.ClientType != application.ClientType
            || updated.ClientId != application.ClientId
            || updated.LoanProductId != application.LoanProductId
            || updated.Amount != application.Amount;
        if (termsChanged && await _db.LoanApplicationClientCodes.AnyAsync(c => c.LoanApplicationId == id, cancellationToken))
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ClientConfirmedTermsLocked);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == updated.LoanProductId, cancellationToken);
        if (product is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ProductNotFound);
        }

        var rangeCheck = CheckAmountAndTerm(updated.Amount, updated.LoanTerm, product);
        if (rangeCheck is not null)
        {
            return new LoanApplicationWriteResult(rangeCheck.Value);
        }

        application.ClientType = updated.ClientType;
        application.LoanPurposeId = updated.LoanPurposeId;
        application.CurrencyId = updated.CurrencyId;
        application.OfficeId = updated.OfficeId;
        application.ClientId = updated.ClientId;
        application.GroupId = updated.GroupId;
        application.LoanProductId = updated.LoanProductId;
        application.Amount = updated.Amount;
        application.LoanTerm = updated.LoanTerm;
        application.LoanTermType = updated.LoanTermType;
        application.Notes = updated.Notes;

        // Older callers don't send the payout details; leave what's there rather than clearing it.
        if (updated.DisbursementMode is not null)
        {
            application.DisbursementMode = updated.DisbursementMode;
            application.DisbursementBankName = updated.DisbursementBankName;
            application.DisbursementAccountNumber = updated.DisbursementAccountNumber;
            application.DisbursementAccountName = updated.DisbursementAccountName;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.Success, application);
    }

    public async Task<LoanApplicationWriteResult> ApproveAsync(int id, decimal approvedAmount, string? notes, CancellationToken cancellationToken = default)
    {
        var application = await _db.LoanApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.NotFound);
        }

        if (application.Status != ApprovalStatus.Pending)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.InvalidTransition);
        }

        var product = await _db.LoanProducts.FirstOrDefaultAsync(p => p.Id == application.LoanProductId, cancellationToken);
        if (product is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ProductNotFound);
        }

        // When loans draw on office funds, the office must be able to cover this after loans already approved.
        var fundsProblem = await _officeFunds.CheckCanApproveAsync(application.OfficeId, approvedAmount, cancellationToken);
        if (fundsProblem is not null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.InsufficientOfficeFunds, Error: fundsProblem);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var attempt = 0; attempt < MaxAccountNumberRetries; attempt++)
        {
            var loan = new Loan
            {
                AccountNumber = await NextAccountNumberAsync(cancellationToken),
                ClientType = application.ClientType,
                LoanProductId = application.LoanProductId,
                ClientId = application.ClientId,
                OfficeId = application.OfficeId,
                GroupId = application.GroupId,
                LoanPurposeId = application.LoanPurposeId,
                CurrencyId = application.CurrencyId,
                AppliedAmount = application.Amount,
                ApprovedAmount = approvedAmount,
                LoanTerm = application.LoanTerm,
                LoanTermType = application.LoanTermType,
                // RepaymentFrequency/Type and the three grace fields aren't captured on the
                // application (only the product has them) — copied here so the Phase 5 schedule
                // generator has everything it needs straight off the Loan record at disbursement.
                RepaymentFrequency = product.RepaymentFrequency,
                RepaymentFrequencyType = product.RepaymentFrequencyType,
                GraceOnPrincipal = product.GraceOnPrincipal,
                GraceOnInterestCharged = product.GraceOnInterestCharged,
                GraceOnInterestPayment = product.GraceOnInterestPayment,
                InterestRate = product.DefaultInterestRate,
                InterestRateType = product.InterestRateType,
                InterestMethod = product.InterestMethod,
                AmortizationMethod = product.AmortizationMethod,
                DisbursementMode = application.DisbursementMode,
                DisbursementBankName = application.DisbursementBankName,
                DisbursementAccountNumber = application.DisbursementAccountNumber,
                DisbursementAccountName = application.DisbursementAccountName,
                Status = LoanStatus.Pending,
                ApprovedById = _currentUser.UserId,
                ApprovedDate = today,
                ApprovedNotes = notes,
                CreatedById = _currentUser.UserId,
                CreatedDate = today,
            };

            LoanCharge? formFeeCharge = null;
            try
            {
                _db.Loans.Add(loan);

                // Setting the navigation (not the LoanId scalar, which is still 0 pre-insert)
                // lets EF's change-tracker fix up application.LoanId to the real generated id
                // as part of this same SaveChangesAsync call.
                application.Loan = loan;
                application.Status = ApprovalStatus.Approved;
                application.ApprovedById = _currentUser.UserId;
                application.ApprovedDate = today;
                application.ApprovedNotes = notes;

                // The form fee stays owed on the loan as a one-time upfront charge.
                if (application.FormFee is > 0)
                {
                    _db.LoanCharges.Add(formFeeCharge = new LoanCharge
                    {
                        Loan = loan,
                        ChargeId = application.FormFeeChargeId,
                        ChargeType = LoanChargeType.Disbursement,
                        ChargeOption = LoanChargeCalculationType.Flat,
                        Amount = application.FormFee,
                        AmountPaid = 0m,
                        DueDate = today,
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);

                return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.Success, application);
            }
            catch (DbUpdateException)
            {
                _db.Entry(loan).State = EntityState.Detached;
                if (formFeeCharge is not null)
                {
                    _db.Entry(formFeeCharge).State = EntityState.Detached;
                }

                application.Loan = null;
                application.LoanId = null;
                application.Status = ApprovalStatus.Pending;
                application.ApprovedById = null;
                application.ApprovedDate = null;
                application.ApprovedNotes = null;
            }
        }

        return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.InvalidTransition);
    }

    public async Task<LoanApplicationWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.ReasonRequired);
        }

        var application = await _db.LoanApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.NotFound);
        }

        if (application.Status != ApprovalStatus.Pending)
        {
            return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.InvalidTransition);
        }

        application.Status = ApprovalStatus.Declined;
        application.DeclinedById = _currentUser.UserId;
        application.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        application.DeclinedNotes = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new LoanApplicationWriteResult(LoanApplicationWriteOutcome.Success, application);
    }

    private static LoanApplicationWriteOutcome? CheckAmountAndTerm(decimal amount, int? term, LoanProduct product)
    {
        if (!LoanProductValidationRules.IsWithinRange(amount, product.MinimumPrincipal, product.MaximumPrincipal))
        {
            return LoanApplicationWriteOutcome.AmountOutOfRange;
        }

        if (term.HasValue && !LoanProductValidationRules.IsWithinRange(term.Value, product.MinimumLoanTerm, product.MaximumLoanTerm))
        {
            return LoanApplicationWriteOutcome.TermOutOfRange;
        }

        return null;
    }

    private async Task<string> NextAccountNumberAsync(CancellationToken cancellationToken)
    {
        var accountNumbers = await _db.Loans.Select(l => l.AccountNumber).ToListAsync(cancellationToken);

        long maxSequence = 0;
        foreach (var accountNumber in accountNumbers)
        {
            if (LoanAccountNumberFormat.TryParseSequence(accountNumber, out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return LoanAccountNumberFormat.Format(maxSequence + 1);
    }
}
