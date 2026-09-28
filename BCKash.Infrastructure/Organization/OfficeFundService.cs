using System.Text.RegularExpressions;
using BCKash.Application.Files;
using BCKash.Application.Organization;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Organization;

public class OfficeFundService : IOfficeFundService
{
    private static readonly Regex Nuban = new(@"^\d{10}$", RegexOptions.Compiled);

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IFileStorageService _files;
    private readonly ICurrencyDisplayProvider _currency;

    public OfficeFundService(BCKashDbContext db, ICurrentUserContext currentUser, IFileStorageService files, ICurrencyDisplayProvider currency)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _currency = currency;
    }

    public async Task<bool> LoansRequireFundsAsync(CancellationToken cancellationToken = default)
    {
        var value = await _db.Settings
            .Where(s => s.SettingKey == IOfficeFundService.RequireFundsSettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return value?.Trim() is "1" or "true";
    }

    public async Task<OfficeFundSummary?> GetSummaryAsync(int officeId, CancellationToken cancellationToken = default)
    {
        var office = await _db.Offices.Where(o => o.Id == officeId).Select(o => new { o.Id, o.ManagerId }).FirstOrDefaultAsync(cancellationToken);
        if (office is null)
        {
            return null;
        }

        var balance = await BalanceAsync(officeId, cancellationToken);
        var committed = await CommittedAsync(officeId, cancellationToken);
        var pending = await _db.OfficeFundings
            .Where(f => f.OfficeId == officeId && (f.Status == OfficeFundingStatus.PendingAcknowledgement || f.Status == OfficeFundingStatus.Disputed))
            .Select(f => f.Amount)
            .ToListAsync(cancellationToken);
        var defaultAccount = await _db.OfficeBankAccounts.FirstOrDefaultAsync(a => a.OfficeId == officeId && a.Active && a.IsDefault, cancellationToken);

        return new OfficeFundSummary(officeId, balance, committed, balance - committed, pending.Sum(), pending.Count, defaultAccount, office.ManagerId,
            await LoansRequireFundsAsync(cancellationToken));
    }

    // ---- Bank accounts -------------------------------------------------------------------------

    public async Task<OfficeFundResult<OfficeBankAccount>> AddBankAccountAsync(
        int officeId, string? bankName, string? accountName, string? accountNumber, bool makeDefault, CancellationToken cancellationToken = default)
    {
        if (!await _db.Offices.AnyAsync(o => o.Id == officeId, cancellationToken))
        {
            return OfficeFundResult<OfficeBankAccount>.Fail(OfficeFundOutcome.NotFound);
        }

        bankName = NigerianBanks.Canonical(bankName);
        accountName = accountName?.Trim();
        accountNumber = accountNumber?.Trim().Replace(" ", string.Empty);
        if (bankName is null) return Invalid<OfficeBankAccount>("Choose the bank from the list of Nigerian banks.");
        if (string.IsNullOrEmpty(accountName) || accountName.Length > 150) return Invalid<OfficeBankAccount>("Enter the account name (up to 150 characters).");
        if (accountNumber is null || !Nuban.IsMatch(accountNumber)) return Invalid<OfficeBankAccount>("The account number must be the 10-digit NUBAN.");

        var accounts = await _db.OfficeBankAccounts.Where(a => a.OfficeId == officeId && a.Active).ToListAsync(cancellationToken);
        if (accounts.Any(a => a.AccountNumber == accountNumber && string.Equals(a.BankName, bankName, StringComparison.OrdinalIgnoreCase)))
        {
            return Invalid<OfficeBankAccount>("This office already has that account.");
        }

        // The first account is always the default — an office must have one.
        var isDefault = makeDefault || accounts.All(a => !a.IsDefault);
        if (isDefault)
        {
            accounts.ForEach(a => a.IsDefault = false);
        }

        var account = new OfficeBankAccount
        {
            OfficeId = officeId, BankName = bankName, AccountName = accountName, AccountNumber = accountNumber,
            IsDefault = isDefault, CreatedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.OfficeBankAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        Log(officeId, OfficeFundEventType.BankAccountAdded, bankAccountId: account.Id, comment: $"{bankName} {Mask(accountNumber)}{(isDefault ? " (default)" : string.Empty)}");
        await _db.SaveChangesAsync(cancellationToken);
        return OfficeFundResult<OfficeBankAccount>.Ok(account);
    }

    public async Task<OfficeFundResult<OfficeBankAccount>> SetDefaultBankAccountAsync(int officeId, int accountId, CancellationToken cancellationToken = default)
    {
        var accounts = await _db.OfficeBankAccounts.Where(a => a.OfficeId == officeId && a.Active).ToListAsync(cancellationToken);
        var account = accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null)
        {
            return OfficeFundResult<OfficeBankAccount>.Fail(OfficeFundOutcome.NotFound);
        }

        if (!account.IsDefault)
        {
            accounts.ForEach(a => { a.IsDefault = a.Id == accountId; a.UpdatedAt = DateTime.UtcNow; });
            Log(officeId, OfficeFundEventType.DefaultAccountChanged, bankAccountId: accountId, comment: $"{account.BankName} {Mask(account.AccountNumber)}");
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OfficeFundResult<OfficeBankAccount>.Ok(account);
    }

    public async Task<OfficeFundResult<OfficeBankAccount>> DeactivateBankAccountAsync(int officeId, int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _db.OfficeBankAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.OfficeId == officeId && a.Active, cancellationToken);
        if (account is null)
        {
            return OfficeFundResult<OfficeBankAccount>.Fail(OfficeFundOutcome.NotFound);
        }

        if (account.IsDefault)
        {
            return Invalid<OfficeBankAccount>("This is the office's default account. Make another account the default first.");
        }

        account.Active = false;
        account.UpdatedAt = DateTime.UtcNow;
        Log(officeId, OfficeFundEventType.BankAccountDeactivated, bankAccountId: accountId, comment: $"{account.BankName} {Mask(account.AccountNumber)}");
        await _db.SaveChangesAsync(cancellationToken);
        return OfficeFundResult<OfficeBankAccount>.Ok(account);
    }

    // ---- Funding -------------------------------------------------------------------------------

    public async Task<OfficeFundResult<OfficeFunding>> FundAsync(int officeId, decimal amount, string? reference, DateOnly? fundedOn, string? notes, CancellationToken cancellationToken = default)
    {
        var office = await _db.Offices.FirstOrDefaultAsync(o => o.Id == officeId, cancellationToken);
        if (office is null)
        {
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.NotFound);
        }

        reference = reference?.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!office.Active) return Invalid<OfficeFunding>("This office is deactivated. Activate it before funding it.");
        if (amount <= 0) return Invalid<OfficeFunding>("The amount must be more than zero.");
        if (string.IsNullOrEmpty(reference) || reference.Length > 100) return Invalid<OfficeFunding>("Enter the bank transfer reference (up to 100 characters).");
        if (fundedOn > today) return Invalid<OfficeFunding>("The funding date can't be in the future.");
        if (office.ManagerId is null) return Invalid<OfficeFunding>("Assign a branch manager to this office first — they have to acknowledge the funding.");
        if (office.ManagerId == _currentUser.UserId) return Invalid<OfficeFunding>("You manage this office, so you can't fund it — another super admin must, and you'll acknowledge it.");

        var defaultAccount = await _db.OfficeBankAccounts.FirstOrDefaultAsync(a => a.OfficeId == officeId && a.Active && a.IsDefault, cancellationToken);
        if (defaultAccount is null) return Invalid<OfficeFunding>("Add the office's default bank account first — that's where the funds are sent.");

        var lowered = reference.ToLower();
        if (await _db.OfficeFundings.AnyAsync(f => f.OfficeId == officeId && f.Reference.ToLower() == lowered, cancellationToken))
        {
            return Invalid<OfficeFunding>("A funding with this transfer reference is already recorded for this office.");
        }

        var funding = new OfficeFunding
        {
            OfficeId = officeId, Amount = amount, Reference = reference, FundedOn = fundedOn ?? today, BankAccountId = defaultAccount.Id,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), FundedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.OfficeFundings.Add(funding);
        await _db.SaveChangesAsync(cancellationToken);

        Log(officeId, OfficeFundEventType.FundingSent, fundingId: funding.Id, amount: amount, comment: funding.Notes);
        await _db.SaveChangesAsync(cancellationToken);
        return OfficeFundResult<OfficeFunding>.Ok(funding);
    }

    public async Task<OfficeFundResult<OfficeFunding>> AcknowledgeAsync(int fundingId, string? comment, CancellationToken cancellationToken = default)
    {
        var (funding, failure) = await LoadForManagerAsync(fundingId, cancellationToken);
        if (failure is not null) return failure;

        if (funding!.Status is not (OfficeFundingStatus.PendingAcknowledgement or OfficeFundingStatus.Disputed))
        {
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.InvalidTransition, "This funding has already been settled.");
        }

        funding.Status = OfficeFundingStatus.Acknowledged;
        funding.AcknowledgedById = _currentUser.UserId;
        funding.AcknowledgedAt = DateTime.UtcNow;
        funding.UpdatedAt = DateTime.UtcNow;

        var fund = await FundRowAsync(funding.OfficeId, cancellationToken);
        fund.Balance += funding.Amount;
        fund.UpdatedAt = DateTime.UtcNow;
        _db.OfficeFundEntries.Add(new OfficeFundEntry
        {
            OfficeId = funding.OfficeId, Type = OfficeFundEntryType.Funding, Amount = funding.Amount, BalanceAfter = fund.Balance,
            FundingId = funding.Id, Description = $"Funding {funding.Reference}", CreatedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow,
        });
        Log(funding.OfficeId, OfficeFundEventType.FundingAcknowledged, fundingId: funding.Id, amount: funding.Amount, comment: comment?.Trim());

        return await SaveGuardedAsync(funding, cancellationToken);
    }

    public async Task<OfficeFundResult<OfficeFunding>> DisputeAsync(int fundingId, string? reason, DisputeUpload? statement, CancellationToken cancellationToken = default)
    {
        var (funding, failure) = await LoadForManagerAsync(fundingId, cancellationToken);
        if (failure is not null) return failure;

        if (funding!.Status != OfficeFundingStatus.PendingAcknowledgement)
        {
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.InvalidTransition, "Only a funding awaiting acknowledgement can be disputed.");
        }

        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 10) return Invalid<OfficeFunding>("Explain why you're disputing this funding (at least 10 characters).");
        if (statement is null || statement.Length == 0) return Invalid<OfficeFunding>("Attach the office's bank statement.");
        if (statement.Length > IOfficeFundService.MaxStatementBytes) return Invalid<OfficeFunding>("The bank statement must be 10 MB or smaller.");
        var extension = Path.GetExtension(statement.FileName).ToLowerInvariant();
        if (!IOfficeFundService.StatementExtensions.Contains(extension)) return Invalid<OfficeFunding>("The bank statement must be a PDF, JPG or PNG.");

        var stored = await _files.SaveAsync(statement.Content, statement.FileName, cancellationToken);
        funding.Status = OfficeFundingStatus.Disputed;
        funding.DisputedById = _currentUser.UserId;
        funding.DisputedAt = DateTime.UtcNow;
        funding.DisputeReason = reason;
        funding.DisputeDocumentName = Path.GetFileName(statement.FileName);
        funding.DisputeDocumentLocation = stored.Location;
        funding.UpdatedAt = DateTime.UtcNow;
        Log(funding.OfficeId, OfficeFundEventType.FundingDisputed, fundingId: funding.Id, amount: funding.Amount, comment: reason);

        var result = await SaveGuardedAsync(funding, cancellationToken);
        if (result.Outcome != OfficeFundOutcome.Success)
        {
            await _files.DeleteAsync(stored.Location, cancellationToken);
        }

        return result;
    }

    public async Task<OfficeFundResult<OfficeFunding>> CancelAsync(int fundingId, string? reason, CancellationToken cancellationToken = default)
    {
        var funding = await _db.OfficeFundings.FirstOrDefaultAsync(f => f.Id == fundingId, cancellationToken);
        if (funding is null)
        {
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.NotFound);
        }

        if (funding.Status is not (OfficeFundingStatus.PendingAcknowledgement or OfficeFundingStatus.Disputed))
        {
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.InvalidTransition, "Only a funding that hasn't been acknowledged can be cancelled.");
        }

        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return Invalid<OfficeFunding>("Give a reason for cancelling.");

        funding.Status = OfficeFundingStatus.Cancelled;
        funding.CancelledById = _currentUser.UserId;
        funding.CancelledAt = DateTime.UtcNow;
        funding.CancelReason = reason;
        funding.UpdatedAt = DateTime.UtcNow;
        Log(funding.OfficeId, OfficeFundEventType.FundingCancelled, fundingId: funding.Id, amount: funding.Amount, comment: reason);
        return await SaveGuardedAsync(funding, cancellationToken);
    }

    // ---- Loans ---------------------------------------------------------------------------------

    public async Task<string?> CheckCanApproveAsync(int? officeId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!await LoansRequireFundsAsync(cancellationToken))
        {
            return null;
        }

        if (officeId is not { } id)
        {
            return "Loans draw on office funds, so the application must belong to an office.";
        }

        var available = await BalanceAsync(id, cancellationToken) - await CommittedAsync(id, cancellationToken);
        if (amount <= available)
        {
            return null;
        }

        var currency = await _currency.GetAsync(cancellationToken);
        return $"The office doesn't have enough funds: {currency.Format(Math.Max(available, 0))} available after loans already approved, and this needs {currency.Format(amount)}. Fund the office first.";
    }

    public async Task<string?> StageDisbursementAsync(Loan loan, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!await LoansRequireFundsAsync(cancellationToken))
        {
            return null;
        }

        if (loan.OfficeId is not { } officeId)
        {
            return "Loans draw on office funds, so the loan must belong to an office.";
        }

        var fund = await FundRowAsync(officeId, cancellationToken);
        if (fund.Balance < amount)
        {
            var currency = await _currency.GetAsync(cancellationToken);
            return $"The office doesn't have enough funds to disburse this loan: {currency.Format(fund.Balance)} available, {currency.Format(amount)} needed.";
        }

        // Balance is a concurrency token: if another disbursement changes it first, the caller's save fails
        // rather than overdrawing the office.
        fund.Balance -= amount;
        fund.UpdatedAt = DateTime.UtcNow;
        _db.OfficeFundEntries.Add(new OfficeFundEntry
        {
            OfficeId = officeId, Type = OfficeFundEntryType.LoanDisbursement, Amount = -amount, BalanceAfter = fund.Balance,
            LoanId = loan.Id, Description = $"Loan {loan.AccountNumber ?? loan.Id.ToString()} disbursed", CreatedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow,
        });
        return null;
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private async Task<decimal> BalanceAsync(int officeId, CancellationToken cancellationToken) =>
        await _db.OfficeFunds.Where(f => f.OfficeId == officeId).Select(f => (decimal?)f.Balance).FirstOrDefaultAsync(cancellationToken) ?? 0;

    /// <summary>Approved, not yet disbursed — loans created by approving an application sit in Pending with an approval date.</summary>
    private async Task<decimal> CommittedAsync(int officeId, CancellationToken cancellationToken) =>
        await _db.Loans
            .Where(l => l.OfficeId == officeId && l.ApprovedDate != null && (l.Status == LoanStatus.Pending || l.Status == LoanStatus.Approved || l.Status == LoanStatus.NeedChanges))
            .SumAsync(l => l.ApprovedAmount ?? l.Principal ?? 0, cancellationToken);

    private async Task<OfficeFund> FundRowAsync(int officeId, CancellationToken cancellationToken)
    {
        var fund = await _db.OfficeFunds.FirstOrDefaultAsync(f => f.OfficeId == officeId, cancellationToken);
        if (fund is null)
        {
            fund = new OfficeFund { OfficeId = officeId, Balance = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _db.OfficeFunds.Add(fund);
        }

        return fund;
    }

    private async Task<(OfficeFunding?, OfficeFundResult<OfficeFunding>?)> LoadForManagerAsync(int fundingId, CancellationToken cancellationToken)
    {
        var funding = await _db.OfficeFundings.FirstOrDefaultAsync(f => f.Id == fundingId, cancellationToken);
        if (funding is null)
        {
            return (null, OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.NotFound));
        }

        var managerId = await _db.Offices.Where(o => o.Id == funding.OfficeId).Select(o => o.ManagerId).FirstOrDefaultAsync(cancellationToken);
        if (managerId is null || managerId != _currentUser.UserId)
        {
            return (null, OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.NotOfficeManager, "Only the office's assigned manager can acknowledge or dispute its funding."));
        }

        if (funding.FundedById == _currentUser.UserId)
        {
            return (null, OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.NotOfficeManager, "You sent this funding, so you can't acknowledge or dispute it."));
        }

        return (funding, null);
    }

    private async Task<OfficeFundResult<OfficeFunding>> SaveGuardedAsync(OfficeFunding funding, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return OfficeFundResult<OfficeFunding>.Ok(funding);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return OfficeFundResult<OfficeFunding>.Fail(OfficeFundOutcome.InvalidTransition, "Someone else updated this funding at the same time. Refresh and try again.");
        }
    }

    private void Log(int officeId, OfficeFundEventType type, int? fundingId = null, int? bankAccountId = null, decimal? amount = null, string? comment = null) =>
        _db.OfficeFundEvents.Add(new OfficeFundEvent
        {
            OfficeId = officeId, Type = type, FundingId = fundingId, BankAccountId = bankAccountId, Amount = amount,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment, ActorId = _currentUser.UserId, CreatedAt = DateTime.UtcNow,
        });

    private static OfficeFundResult<T> Invalid<T>(string error) => OfficeFundResult<T>.Fail(OfficeFundOutcome.Invalid, error);

    private static string Mask(string accountNumber) => accountNumber.Length > 4 ? $"••••{accountNumber[^4..]}" : accountNumber;
}
