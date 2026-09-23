using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;

namespace BCKash.Application.GeneralLedger;

public record TrialBalanceRow(int GlAccountId, string? Name, string? GlCode, string AccountType, decimal TotalDebit, decimal TotalCredit);

public record AccountTypeBalance(string AccountType, decimal TotalDebit, decimal TotalCredit, decimal Net);

public record ProfitAndLossResult(IReadOnlyList<AccountTypeBalance> Sections, decimal NetProfit);

public record CashFlowPeriod(string Period, decimal NetMovement);

public record ProvisioningRow(string? CriteriaName, int? MinDays, int? MaxDays, int? Percentage, decimal OutstandingBalance, decimal RequiredProvision);

public record HistoricalIncomeStatementPeriod(string Period, decimal Income, decimal Expense, decimal NetProfit);

public record JournalEntryRow(DateOnly? Date, string? Reference, string? AccountName, decimal? Debit, decimal? Credit, string? TransactionType, string? Narration);

public record AccruedInterestRow(int? OfficeId, decimal TotalAccruedInterest);

/// <summary>
/// FR-GL-7: computed from posted, non-reversed journal entries only (`Approved == true &&
/// Reversed == false`) — a manual entry pending approval never affects these figures. Cash
/// flow is explicitly a simplified view (net period-over-period movement across Asset-type
/// accounts), not a full indirect/direct-method statement — the phase spec allowed "basic/
/// unstyled" here; that polish was never picked up, so it stays simplified in Phase 9 too.
/// Phase 9 (FR-RPT-1/BR-RPT-1) adds the four remaining FRD §14 financial reports:
/// Provisioning, Historical Income Statement, Journals Report, and Accrued Interest — see
/// docs/phase9-communications-reporting-spec.md for what each one computes and why.
/// </summary>
public interface IGlReportService
{
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountTypeBalance>> GetBalanceSheetAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    Task<ProfitAndLossResult> GetProfitAndLossAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashFlowPeriod>> GetCashFlowAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Buckets each disbursed loan's outstanding principal balance into the matching
    /// <see cref="BCKash.Domain.Loans.LoanProvisioningCriteria"/> band (by days-in-arrears,
    /// reusing LoanNpaService's own days-in-arrears computation) and applies that band's
    /// configured percentage.
    /// </summary>
    Task<IReadOnlyList<ProvisioningRow>> GetProvisioningAsync(int? officeId, CancellationToken cancellationToken = default);

    /// <summary>Same Income/Expense figures as <see cref="GetProfitAndLossAsync"/>, broken down by month.</summary>
    Task<IReadOnlyList<HistoricalIncomeStatementPeriod>> GetHistoricalIncomeStatementAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    /// <summary>Raw posted journal entries in date order — the GL's own transaction log.</summary>
    Task<IReadOnlyList<JournalEntryRow>> GetJournalsAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums posted GlTransactionType.InterestAccrual/FeeAccrual entries by office. Legitimately
    /// empty today — nothing in this codebase posts accrual entries yet (see
    /// docs/gl-posting-spec.md's "what's out of scope" section) — not a stub, just an honest
    /// reflection of the system's current capabilities.
    /// </summary>
    Task<IReadOnlyList<AccruedInterestRow>> GetAccruedInterestAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    /// <summary>Dispatches to whichever of the eight financial-report methods above matches <paramref name="reportName"/>, wrapped into the generic tabular <see cref="ReportResult"/> shape used by the report catalog/scheduler.</summary>
    Task<ReportResult> RunFinancialReportAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
