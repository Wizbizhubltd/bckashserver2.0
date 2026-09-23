using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Savings account, savings balance, savings transaction, and fixed term maturity reports
/// (FR-RPT-1). Column sets are a documented modeling choice — see
/// docs/phase9-communications-reporting-spec.md.
/// </summary>
public class SavingsReportService : ISavingsReportService
{
    private static readonly ScheduledReportName[] Handled =
    [
        ScheduledReportName.SavingsAccountReport, ScheduledReportName.SavingsBalanceReport,
        ScheduledReportName.SavingsTransactionReport, ScheduledReportName.FixedTermMaturityReport,
    ];

    private readonly BCKashDbContext _db;

    public SavingsReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public bool CanHandle(ScheduledReportName reportName) => Handled.Contains(reportName);

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default) => reportName switch
    {
        ScheduledReportName.SavingsAccountReport => SavingsAccountAsync(filter, cancellationToken),
        ScheduledReportName.SavingsBalanceReport => SavingsBalanceAsync(filter, cancellationToken),
        ScheduledReportName.SavingsTransactionReport => SavingsTransactionAsync(filter, cancellationToken),
        ScheduledReportName.FixedTermMaturityReport => FixedTermMaturityAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
    };

    private IQueryable<SavingsAccount> FilteredAccounts(ReportFilter filter)
    {
        var query = _db.Savings.AsQueryable();
        if (filter.OfficeId.HasValue)
        {
            query = query.Where(s => s.OfficeId == filter.OfficeId);
        }

        return query;
    }

    private async Task<ReportResult> SavingsAccountAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var accounts = await FilteredAccounts(filter)
            .Select(s => new
            {
                s.AccountNumber,
                ClientName = _db.Clients.Where(c => c.Id == s.ClientId).Select(c => c.FullName ?? c.DisplayName).FirstOrDefault(),
                Product = s.SavingsProduct != null ? s.SavingsProduct.Name : null,
                s.Status,
                s.Balance,
            })
            .OrderBy(s => s.AccountNumber)
            .ToListAsync(cancellationToken);

        var rows = accounts.Select(a => (IReadOnlyList<string?>)[a.AccountNumber, a.ClientName, a.Product, a.Status.ToString(), (a.Balance ?? 0m).ToString("F2")]).ToList();

        return new ReportResult(ScheduledReportName.SavingsAccountReport, ["Account No", "Client", "Product", "Status", "Balance"], rows);
    }

    private async Task<ReportResult> SavingsBalanceAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var totals = await FilteredAccounts(filter)
            .GroupBy(s => s.SavingsProductId)
            .Select(g => new
            {
                Product = g.Select(s => s.SavingsProduct != null ? s.SavingsProduct.Name : null).FirstOrDefault(),
                AccountCount = g.Count(),
                TotalBalance = g.Sum(s => s.Balance ?? 0m),
            })
            .ToListAsync(cancellationToken);

        var rows = totals.Select(t => (IReadOnlyList<string?>)[t.Product, t.AccountCount.ToString(), t.TotalBalance.ToString("F2")]).ToList();

        return new ReportResult(ScheduledReportName.SavingsBalanceReport, ["Product", "Account Count", "Total Balance"], rows);
    }

    private async Task<ReportResult> SavingsTransactionAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var accountIds = await FilteredAccounts(filter).Select(s => s.Id).ToListAsync(cancellationToken);

        var query = _db.SavingsTransactions.Where(t => accountIds.Contains(t.SavingsId ?? 0) && !t.Reversed);
        if (filter.FromDate.HasValue)
        {
            query = query.Where(t => t.Date >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(t => t.Date <= filter.ToDate);
        }

        var transactions = await query
            .Select(t => new
            {
                AccountNumber = _db.Savings.Where(s => s.Id == t.SavingsId).Select(s => s.AccountNumber).FirstOrDefault(),
                t.Date,
                t.TransactionType,
                t.Amount,
                t.Balance,
            })
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var rows = transactions.Select(t => (IReadOnlyList<string?>)
            [t.AccountNumber, t.Date?.ToString("yyyy-MM-dd"), t.TransactionType?.ToString(), (t.Amount ?? 0m).ToString("F2"), (t.Balance ?? 0m).ToString("F2")]).ToList();

        return new ReportResult(ScheduledReportName.SavingsTransactionReport, ["Account No", "Date", "Type", "Amount", "Balance"], rows);
    }

    /// <summary>
    /// This schema has no fixed-term/maturity-dated savings concept — SavingsAccount/SavingsProduct
    /// carry no maturity date, term length, or "fixed deposit" flag at all, only regular
    /// passbook-style accounts. Always returns an empty (but real, not stubbed) result.
    /// </summary>
    private static Task<ReportResult> FixedTermMaturityAsync() =>
        Task.FromResult(new ReportResult(ScheduledReportName.FixedTermMaturityReport, ["Account No", "Client", "Maturity Date", "Maturity Amount"], []));
}
