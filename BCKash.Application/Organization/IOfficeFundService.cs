using BCKash.Domain.Loans;
using BCKash.Domain.Organization;

namespace BCKash.Application.Organization;

public enum OfficeFundOutcome
{
    Success,
    NotFound,

    /// <summary>Breaks a rule — see <see cref="OfficeFundResult{T}.Error"/>.</summary>
    Invalid,

    /// <summary>Only the office's assigned manager can do this.</summary>
    NotOfficeManager,

    /// <summary>The funding isn't in a state this action applies to (or changed concurrently).</summary>
    InvalidTransition,
}

public record OfficeFundResult<T>(OfficeFundOutcome Outcome, T? Value = default, string? Error = null)
{
    public static OfficeFundResult<T> Ok(T value) => new(OfficeFundOutcome.Success, value);
    public static OfficeFundResult<T> Fail(OfficeFundOutcome outcome, string? error = null) => new(outcome, default, error);
}

/// <summary>An office's money position.</summary>
/// <param name="Balance">Acknowledged funding less loans disbursed.</param>
/// <param name="Committed">Loans approved but not yet disbursed — already spoken for.</param>
/// <param name="Available">What new loans can still be approved against: Balance − Committed.</param>
public record OfficeFundSummary(
    int OfficeId, decimal Balance, decimal Committed, decimal Available,
    decimal PendingAmount, int PendingCount, OfficeBankAccount? DefaultAccount, int? ManagerId, bool LoansRequireFunds);

public record DisputeUpload(Stream Content, string FileName, long Length);

/// <summary>
/// Office business operations: bank accounts, super-admin funding acknowledged or disputed by the
/// office's manager, and the office fund that approved loans are drawn from. When "Loans draw on
/// office funds" is on (Settings → Loan), an application can only be approved if the office's
/// available funds cover it, and disbursing deducts from the office's balance.
/// </summary>
public interface IOfficeFundService
{
    public const string RequireFundsSettingKey = "loan_requires_office_funds";
    public const long MaxStatementBytes = 10 * 1024 * 1024;
    public static readonly string[] StatementExtensions = [".pdf", ".png", ".jpg", ".jpeg"];

    Task<bool> LoansRequireFundsAsync(CancellationToken cancellationToken = default);

    Task<OfficeFundSummary?> GetSummaryAsync(int officeId, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeBankAccount>> AddBankAccountAsync(int officeId, string? bankName, string? accountName, string? accountNumber, bool makeDefault, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeBankAccount>> SetDefaultBankAccountAsync(int officeId, int accountId, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeBankAccount>> DeactivateBankAccountAsync(int officeId, int accountId, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeFunding>> FundAsync(int officeId, decimal amount, string? reference, DateOnly? fundedOn, string? notes, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeFunding>> AcknowledgeAsync(int fundingId, string? comment, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeFunding>> DisputeAsync(int fundingId, string? reason, DisputeUpload? statement, CancellationToken cancellationToken = default);

    Task<OfficeFundResult<OfficeFunding>> CancelAsync(int fundingId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Null when <paramref name="amount"/> can be approved for the office (or funds aren't required); otherwise why not.</summary>
    Task<string?> CheckCanApproveAsync(int? officeId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the deduction for disbursing <paramref name="loan"/> (the caller's SaveChanges commits it).
    /// Null when fine (or funds aren't required); otherwise why the loan can't be disbursed.
    /// </summary>
    Task<string?> StageDisbursementAsync(Loan loan, decimal amount, CancellationToken cancellationToken = default);
}
