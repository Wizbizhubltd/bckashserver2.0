using BCKash.Application.GeneralLedger;
using BCKash.Application.Reporting;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

public class GlReportService : IGlReportService
{
    private readonly BCKashDbContext _db;

    public GlReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);

        return entries
            .GroupBy(e => e.GlAccountId)
            .Select(g => new TrialBalanceRow(
                g.Key ?? 0,
                g.First().GlAccount?.Name,
                g.First().GlAccount?.GlCode,
                (g.First().GlAccount?.AccountType ?? GlAccountType.Asset).ToString(),
                g.Sum(e => e.Debit ?? 0m),
                g.Sum(e => e.Credit ?? 0m)))
            .OrderBy(r => r.GlCode)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountTypeBalance>> GetBalanceSheetAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);
        var types = new[] { GlAccountType.Asset, GlAccountType.Liability, GlAccountType.Equity };

        return BalancesByType(entries, types);
    }

    public async Task<ProfitAndLossResult> GetProfitAndLossAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);
        var sections = BalancesByType(entries, [GlAccountType.Income, GlAccountType.Expense]);

        var income = sections.FirstOrDefault(s => s.AccountType == nameof(GlAccountType.Income))?.Net ?? 0m;
        var expense = sections.FirstOrDefault(s => s.AccountType == nameof(GlAccountType.Expense))?.Net ?? 0m;

        return new ProfitAndLossResult(sections, income - expense);
    }

    /// <summary>Debit-normal types (Asset, Expense) report Net = debit - credit; credit-normal types (Liability, Equity, Income) report Net = credit - debit — the standard sign convention, so a P&amp;L's income/expense figures and NetProfit come out positive in the ordinary case rather than requiring the reader to mentally flip signs.</summary>
    private static bool IsDebitNormal(GlAccountType type) => type is GlAccountType.Asset or GlAccountType.Expense;

    /// <summary>
    /// Simplified — net period-over-period movement across Asset-type accounts, grouped by
    /// month. Not a full indirect/direct-method cash flow statement; the phase spec explicitly
    /// allows "basic/unstyled" here, with polish deferred to Phase 8's reporting pass.
    /// </summary>
    public async Task<IReadOnlyList<CashFlowPeriod>> GetCashFlowAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);

        return entries
            .Where(e => e.GlAccount?.AccountType == GlAccountType.Asset && e.Date.HasValue)
            .GroupBy(e => $"{e.Date!.Value.Year:D4}-{e.Date!.Value.Month:D2}")
            .OrderBy(g => g.Key)
            .Select(g => new CashFlowPeriod(g.Key, g.Sum(e => (e.Debit ?? 0m) - (e.Credit ?? 0m))))
            .ToList();
    }

    /// <summary>
    /// Buckets each disbursed loan's outstanding principal into the matching provisioning
    /// band. A loan not covered by any configured band (days-in-arrears outside every
    /// Min..Max range, or no criteria configured at all) is simply left out — same
    /// "don't post/report something wrong" treatment as GL posting's unconfigured-account case.
    /// </summary>
    public async Task<IReadOnlyList<ProvisioningRow>> GetProvisioningAsync(int? officeId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var criteria = await _db.LoanProvisioningCriteria.Where(c => c.Active).ToListAsync(cancellationToken);
        if (criteria.Count == 0)
        {
            return [];
        }

        var loansQuery = _db.Loans.Where(l => l.Status == Domain.Loans.LoanStatus.Disbursed);
        if (officeId.HasValue)
        {
            loansQuery = loansQuery.Where(l => l.OfficeId == officeId);
        }

        var loanIds = await loansQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        var perLoan = await _db.LoanRepaymentSchedules
            .Where(s => loanIds.Contains(s.LoanId ?? 0))
            .GroupBy(s => s.LoanId)
            .Select(g => new
            {
                LoanId = g.Key,
                Outstanding = g.Sum(s => (s.Principal ?? 0m) - (s.PrincipalPaid ?? 0m) - (s.PrincipalWaived ?? 0m) - (s.PrincipalWrittenOff ?? 0m)),
                OldestUnpaidDueDate = g.Where(s => !s.Paid).Min(s => (DateOnly?)s.DueDate),
            })
            .ToListAsync(cancellationToken);

        var rows = new List<ProvisioningRow>();
        foreach (var band in criteria)
        {
            var inBand = perLoan.Where(l =>
            {
                var daysInArrears = l.OldestUnpaidDueDate.HasValue ? today.DayNumber - l.OldestUnpaidDueDate.Value.DayNumber : 0;
                return daysInArrears >= (band.Min ?? 0) && daysInArrears <= (band.Max ?? int.MaxValue);
            }).ToList();

            var outstanding = inBand.Sum(l => l.Outstanding);
            var provision = outstanding * (band.Percentage ?? 0) / 100m;
            rows.Add(new ProvisioningRow(band.Name, band.Min, band.Max, band.Percentage, outstanding, provision));
        }

        return rows;
    }

    public async Task<IReadOnlyList<HistoricalIncomeStatementPeriod>> GetHistoricalIncomeStatementAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);

        return entries
            .Where(e => e.Date.HasValue && e.GlAccount != null && e.GlAccount.AccountType is GlAccountType.Income or GlAccountType.Expense)
            .GroupBy(e => $"{e.Date!.Value.Year:D4}-{e.Date!.Value.Month:D2}")
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var income = g.Where(e => e.GlAccount!.AccountType == GlAccountType.Income).Sum(e => (e.Credit ?? 0m) - (e.Debit ?? 0m));
                var expense = g.Where(e => e.GlAccount!.AccountType == GlAccountType.Expense).Sum(e => (e.Debit ?? 0m) - (e.Credit ?? 0m));
                return new HistoricalIncomeStatementPeriod(g.Key, income, expense, income - expense);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<JournalEntryRow>> GetJournalsAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);

        return entries
            .OrderBy(e => e.Date)
            .Select(e => new JournalEntryRow(e.Date, e.Reference, e.GlAccount?.Name, e.Debit, e.Credit, e.TransactionType?.ToString(), e.Narration))
            .ToList();
    }

    /// <summary>Legitimately empty today — see the interface doc comment for why.</summary>
    public async Task<IReadOnlyList<AccruedInterestRow>> GetAccruedInterestAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var entries = await PostedEntriesAsync(officeId, fromDate, toDate, cancellationToken);

        return entries
            .Where(e => e.TransactionType is GlTransactionType.InterestAccrual or GlTransactionType.FeeAccrual)
            .GroupBy(e => e.OfficeId)
            .Select(g => new AccruedInterestRow(g.Key, g.Sum(e => (e.Debit ?? 0m) - (e.Credit ?? 0m))))
            .ToList();
    }

    public async Task<ReportResult> RunFinancialReportAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default)
    {
        switch (reportName)
        {
            case ScheduledReportName.TrialBalance:
                var tb = await GetTrialBalanceAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Account", "GL Code", "Account Type", "Total Debit", "Total Credit"],
                    tb.Select(r => (IReadOnlyList<string?>)[r.Name, r.GlCode, r.AccountType, r.TotalDebit.ToString("F2"), r.TotalCredit.ToString("F2")]).ToList());

            case ScheduledReportName.BalanceSheet:
                var bs = await GetBalanceSheetAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Account Type", "Total Debit", "Total Credit", "Net"],
                    bs.Select(r => (IReadOnlyList<string?>)[r.AccountType, r.TotalDebit.ToString("F2"), r.TotalCredit.ToString("F2"), r.Net.ToString("F2")]).ToList());

            case ScheduledReportName.ProfitAndLoss:
                var pl = await GetProfitAndLossAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                var plRows = pl.Sections.Select(r => (IReadOnlyList<string?>)[r.AccountType, r.TotalDebit.ToString("F2"), r.TotalCredit.ToString("F2"), r.Net.ToString("F2")]).ToList();
                plRows.Add(["Net Profit", null, null, pl.NetProfit.ToString("F2")]);
                return new ReportResult(reportName, ["Section", "Total Debit", "Total Credit", "Net"], plRows);

            case ScheduledReportName.CashFlow:
                var cf = await GetCashFlowAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Period", "Net Movement"], cf.Select(r => (IReadOnlyList<string?>)[r.Period, r.NetMovement.ToString("F2")]).ToList());

            case ScheduledReportName.Provisioning:
                var pr = await GetProvisioningAsync(filter.OfficeId, cancellationToken);
                return new ReportResult(reportName, ["Criteria", "Min Days", "Max Days", "Percentage", "Outstanding", "Required Provision"],
                    pr.Select(r => (IReadOnlyList<string?>)[r.CriteriaName, r.MinDays?.ToString(), r.MaxDays?.ToString(), r.Percentage?.ToString(), r.OutstandingBalance.ToString("F2"), r.RequiredProvision.ToString("F2")]).ToList());

            case ScheduledReportName.HistoricalIncomeStatement:
                var his = await GetHistoricalIncomeStatementAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Period", "Income", "Expense", "Net Profit"],
                    his.Select(r => (IReadOnlyList<string?>)[r.Period, r.Income.ToString("F2"), r.Expense.ToString("F2"), r.NetProfit.ToString("F2")]).ToList());

            case ScheduledReportName.JournalsReport:
                var journals = await GetJournalsAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Date", "Reference", "Account", "Debit", "Credit", "Type", "Narration"],
                    journals.Select(r => (IReadOnlyList<string?>)[r.Date?.ToString("yyyy-MM-dd"), r.Reference, r.AccountName, r.Debit?.ToString("F2"), r.Credit?.ToString("F2"), r.TransactionType, r.Narration]).ToList());

            case ScheduledReportName.AccruedInterest:
                var accrued = await GetAccruedInterestAsync(filter.OfficeId, filter.FromDate, filter.ToDate, cancellationToken);
                return new ReportResult(reportName, ["Office", "Total Accrued Interest"],
                    accrued.Select(r => (IReadOnlyList<string?>)[r.OfficeId?.ToString(), r.TotalAccruedInterest.ToString("F2")]).ToList());

            default:
                throw new ArgumentOutOfRangeException(nameof(reportName));
        }
    }

    private async Task<List<GlJournalEntry>> PostedEntriesAsync(int? officeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var query = _db.GlJournalEntries.Include(e => e.GlAccount).Where(e => e.Approved && !e.Reversed);

        if (officeId.HasValue)
        {
            query = query.Where(e => e.OfficeId == officeId);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.Date >= fromDate);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.Date <= toDate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static List<AccountTypeBalance> BalancesByType(IReadOnlyList<GlJournalEntry> entries, IReadOnlyList<GlAccountType> types) =>
        types.Select(type =>
        {
            var forType = entries.Where(e => e.GlAccount?.AccountType == type).ToList();
            var debit = forType.Sum(e => e.Debit ?? 0m);
            var credit = forType.Sum(e => e.Credit ?? 0m);
            var net = IsDebitNormal(type) ? debit - credit : credit - debit;
            return new AccountTypeBalance(type.ToString(), debit, credit, net);
        }).ToList();
}
