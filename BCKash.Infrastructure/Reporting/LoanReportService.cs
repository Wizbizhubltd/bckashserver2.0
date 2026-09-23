using BCKash.Application.Reporting;
using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Disbursed loans, loan portfolio, expected repayments, repayments, collection, arrears, loan
/// sizes, individual indicator, and loan officer performance reports (FR-RPT-1). "Days in
/// arrears" reuses the same definition as LoanNpaService: today minus the oldest unpaid
/// schedule row's due date. Column sets are a documented modeling choice (no per-report spec
/// exists in the FRD) — see docs/phase9-communications-reporting-spec.md.
/// </summary>
public class LoanReportService : ILoanReportService
{
    private static readonly ScheduledReportName[] Handled =
    [
        ScheduledReportName.DisbursedLoansReport, ScheduledReportName.LoanPortfolioReport, ScheduledReportName.ExpectedRepaymentsReport,
        ScheduledReportName.RepaymentsReport, ScheduledReportName.CollectionReport, ScheduledReportName.ArrearsReport,
        ScheduledReportName.LoanSizesReport, ScheduledReportName.IndividualIndicatorReport, ScheduledReportName.LoanOfficerPerformanceReport,
    ];

    private readonly BCKashDbContext _db;

    public LoanReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public bool CanHandle(ScheduledReportName reportName) => Handled.Contains(reportName);

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default) => reportName switch
    {
        ScheduledReportName.DisbursedLoansReport => DisbursedLoansAsync(filter, cancellationToken),
        ScheduledReportName.LoanPortfolioReport => LoanPortfolioAsync(filter, cancellationToken),
        ScheduledReportName.ExpectedRepaymentsReport => ExpectedRepaymentsAsync(filter, cancellationToken),
        ScheduledReportName.RepaymentsReport => RepaymentsAsync(filter, cancellationToken),
        ScheduledReportName.CollectionReport => CollectionAsync(filter, cancellationToken),
        ScheduledReportName.ArrearsReport => ArrearsAsync(filter, cancellationToken),
        ScheduledReportName.LoanSizesReport => LoanSizesAsync(filter, cancellationToken),
        ScheduledReportName.IndividualIndicatorReport => IndividualIndicatorAsync(filter, cancellationToken),
        ScheduledReportName.LoanOfficerPerformanceReport => LoanOfficerPerformanceAsync(filter, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
    };

    private IQueryable<Loan> FilteredLoans(ReportFilter filter, bool applyLoanStatusAndProduct = true)
    {
        var query = _db.Loans.AsQueryable();

        if (filter.OfficeId.HasValue)
        {
            query = query.Where(l => l.OfficeId == filter.OfficeId);
        }

        if (filter.LoanOfficerId.HasValue)
        {
            query = query.Where(l => l.LoanOfficerId == filter.LoanOfficerId);
        }

        if (applyLoanStatusAndProduct)
        {
            if (filter.LoanStatus.HasValue)
            {
                query = query.Where(l => l.Status == filter.LoanStatus);
            }

            if (filter.LoanProductId.HasValue)
            {
                query = query.Where(l => l.LoanProductId == filter.LoanProductId);
            }
        }

        return query;
    }

    private async Task<ReportResult> DisbursedLoansAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = FilteredLoans(filter).Where(l => l.DisbursementDate != null);

        if (filter.FromDate.HasValue)
        {
            query = query.Where(l => l.DisbursementDate >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(l => l.DisbursementDate <= filter.ToDate);
        }

        var loans = await query
            .Select(l => new
            {
                l.AccountNumber,
                ClientName = _db.Clients.Where(c => c.Id == l.ClientId).Select(c => c.FullName ?? c.DisplayName).FirstOrDefault(),
                Product = l.LoanProduct != null ? l.LoanProduct.Name : null,
                l.ApprovedAmount,
                l.DisbursementDate,
                LoanOfficer = _db.Users.Where(u => u.Id == l.LoanOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            })
            .OrderBy(l => l.DisbursementDate)
            .ToListAsync(cancellationToken);

        var rows = loans.Select(l => (IReadOnlyList<string?>)
            [l.AccountNumber, l.ClientName, l.Product, l.ApprovedAmount?.ToString("F2"), l.DisbursementDate?.ToString("yyyy-MM-dd"), l.LoanOfficer]).ToList();

        return new ReportResult(ScheduledReportName.DisbursedLoansReport, ["Account No", "Client", "Product", "Amount", "Disbursement Date", "Loan Officer"], rows);
    }

    /// <summary>Outstanding principal = scheduled principal minus paid/waived/written-off, summed per product, for currently-disbursed loans.</summary>
    private async Task<ReportResult> LoanPortfolioAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Where(l => l.Status == LoanStatus.Disbursed).Select(l => l.Id).ToListAsync(cancellationToken);

        var byProduct = await _db.Loans
            .Where(l => loanIds.Contains(l.Id))
            .GroupJoin(_db.LoanRepaymentSchedules, l => l.Id, s => s.LoanId, (l, schedules) => new { l, schedules })
            .Select(x => new
            {
                Product = x.l.LoanProduct != null ? x.l.LoanProduct.Name : null,
                LoanId = x.l.Id,
                Outstanding = x.schedules.Sum(s =>
                    (s.Principal ?? 0m) - (s.PrincipalPaid ?? 0m) - (s.PrincipalWaived ?? 0m) - (s.PrincipalWrittenOff ?? 0m)),
            })
            .GroupBy(x => x.Product)
            .Select(g => new { Product = g.Key, LoanCount = g.Select(x => x.LoanId).Distinct().Count(), Outstanding = g.Sum(x => x.Outstanding) })
            .ToListAsync(cancellationToken);

        var rows = byProduct.Select(g => (IReadOnlyList<string?>)[g.Product, g.LoanCount.ToString(), g.Outstanding.ToString("F2")]).ToList();

        return new ReportResult(ScheduledReportName.LoanPortfolioReport, ["Product", "Active Loans", "Outstanding Principal"], rows);
    }

    private async Task<ReportResult> ExpectedRepaymentsAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);

        var query = _db.LoanRepaymentSchedules.Where(s => loanIds.Contains(s.LoanId ?? 0) && !s.Paid);

        if (filter.FromDate.HasValue)
        {
            query = query.Where(s => s.DueDate >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(s => s.DueDate <= filter.ToDate);
        }

        var schedules = await query
            .Select(s => new
            {
                AccountNumber = _db.Loans.Where(l => l.Id == s.LoanId).Select(l => l.AccountNumber).FirstOrDefault(),
                s.DueDate,
                Principal = s.Principal ?? 0m,
                Interest = s.Interest ?? 0m,
                Total = (s.Principal ?? 0m) + (s.Interest ?? 0m) + (s.Fees ?? 0m) + (s.Penalty ?? 0m),
            })
            .OrderBy(s => s.DueDate)
            .ToListAsync(cancellationToken);

        var rows = schedules.Select(s => (IReadOnlyList<string?>)
            [s.AccountNumber, s.DueDate?.ToString("yyyy-MM-dd"), s.Principal.ToString("F2"), s.Interest.ToString("F2"), s.Total.ToString("F2")]).ToList();

        return new ReportResult(ScheduledReportName.ExpectedRepaymentsReport, ["Account No", "Due Date", "Principal", "Interest", "Total Due"], rows);
    }

    private async Task<ReportResult> RepaymentsAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);

        var query = _db.LoanTransactions.Where(t => loanIds.Contains(t.LoanId ?? 0) && t.TransactionType == LoanTransactionType.Repayment && !t.Reversed);

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
                AccountNumber = _db.Loans.Where(l => l.Id == t.LoanId).Select(l => l.AccountNumber).FirstOrDefault(),
                t.Date,
                Principal = t.Principal ?? 0m,
                Interest = t.Interest ?? 0m,
                Fee = t.Fee ?? 0m,
                Penalty = t.Penalty ?? 0m,
                Total = t.Amount ?? 0m,
            })
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var rows = transactions.Select(t => (IReadOnlyList<string?>)
        [
            t.AccountNumber, t.Date?.ToString("yyyy-MM-dd"), t.Principal.ToString("F2"), t.Interest.ToString("F2"),
            t.Fee.ToString("F2"), t.Penalty.ToString("F2"), t.Total.ToString("F2"),
        ]).ToList();

        return new ReportResult(ScheduledReportName.RepaymentsReport, ["Account No", "Date", "Principal", "Interest", "Fee", "Penalty", "Total"], rows);
    }

    /// <summary>Expected (scheduled due) vs. collected (actual repayment transactions) within the period, per office.</summary>
    private async Task<ReportResult> CollectionAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);

        var expectedQuery = _db.LoanRepaymentSchedules.Where(s => loanIds.Contains(s.LoanId ?? 0));
        if (filter.FromDate.HasValue)
        {
            expectedQuery = expectedQuery.Where(s => s.DueDate >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            expectedQuery = expectedQuery.Where(s => s.DueDate <= filter.ToDate);
        }

        var expected = await expectedQuery.SumAsync(s => (s.Principal ?? 0m) + (s.Interest ?? 0m) + (s.Fees ?? 0m) + (s.Penalty ?? 0m), cancellationToken);

        var collectedQuery = _db.LoanTransactions.Where(t => loanIds.Contains(t.LoanId ?? 0) && t.TransactionType == LoanTransactionType.Repayment && !t.Reversed);
        if (filter.FromDate.HasValue)
        {
            collectedQuery = collectedQuery.Where(t => t.Date >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            collectedQuery = collectedQuery.Where(t => t.Date <= filter.ToDate);
        }

        var collected = await collectedQuery.SumAsync(t => t.Amount ?? 0m, cancellationToken);
        var rate = expected == 0 ? 0 : Math.Round(collected / expected * 100m, 2);

        return new ReportResult(
            ScheduledReportName.CollectionReport,
            ["Expected", "Collected", "Collection Rate %"],
            [[expected.ToString("F2"), collected.ToString("F2"), rate.ToString("F2")]]);
    }

    private async Task<ReportResult> ArrearsAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var overdue = await _db.LoanRepaymentSchedules
            .Where(s => loanIds.Contains(s.LoanId ?? 0) && !s.Paid && s.DueDate < today)
            .GroupBy(s => s.LoanId)
            .Select(g => new
            {
                LoanId = g.Key,
                OldestDueDate = g.Min(s => s.DueDate),
                OverdueAmount = g.Sum(s =>
                    (s.Principal ?? 0m) - (s.PrincipalPaid ?? 0m) - (s.PrincipalWaived ?? 0m) - (s.PrincipalWrittenOff ?? 0m) +
                    (s.Interest ?? 0m) - (s.InterestPaid ?? 0m) - (s.InterestWaived ?? 0m) - (s.InterestWrittenOff ?? 0m)),
            })
            .ToListAsync(cancellationToken);

        var loanInfo = await _db.Loans
            .Where(l => overdue.Select(o => o.LoanId).Contains(l.Id))
            .Select(l => new
            {
                l.Id,
                l.AccountNumber,
                ClientName = _db.Clients.Where(c => c.Id == l.ClientId).Select(c => c.FullName ?? c.DisplayName).FirstOrDefault(),
                LoanOfficer = _db.Users.Where(u => u.Id == l.LoanOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var rows = overdue.Select(o =>
        {
            loanInfo.TryGetValue(o.LoanId ?? 0, out var info);
            var daysInArrears = o.OldestDueDate.HasValue ? today.DayNumber - o.OldestDueDate.Value.DayNumber : 0;
            return (IReadOnlyList<string?>)[info?.AccountNumber, info?.ClientName, daysInArrears.ToString(), o.OverdueAmount.ToString("F2"), info?.LoanOfficer];
        }).OrderByDescending(r => int.Parse(r[2]!)).ToList();

        return new ReportResult(ScheduledReportName.ArrearsReport, ["Account No", "Client", "Days In Arrears", "Overdue Amount", "Loan Officer"], rows);
    }

    private async Task<ReportResult> LoanSizesAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var amounts = await FilteredLoans(filter).Where(l => l.ApprovedAmount != null).Select(l => l.ApprovedAmount!.Value).ToListAsync(cancellationToken);

        (string Band, decimal Min, decimal Max)[] bands =
        [
            ("0 - 50,000", 0, 50_000), ("50,001 - 200,000", 50_000, 200_000),
            ("200,001 - 1,000,000", 200_000, 1_000_000), ("1,000,001+", 1_000_000, decimal.MaxValue),
        ];

        var rows = bands.Select(b =>
        {
            var inBand = amounts.Where(a => a > b.Min && a <= b.Max).ToList();
            return (IReadOnlyList<string?>)[b.Band, inBand.Count.ToString(), inBand.Sum().ToString("F2")];
        }).ToList();

        return new ReportResult(ScheduledReportName.LoanSizesReport, ["Size Band", "Loan Count", "Total Amount"], rows);
    }

    /// <summary>Per client: active loan count, total disbursed, and current overdue amount.</summary>
    private async Task<ReportResult> IndividualIndicatorAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);

        var overdueByLoan = await _db.LoanRepaymentSchedules
            .Where(s => loanIds.Contains(s.LoanId ?? 0) && !s.Paid && s.DueDate < today)
            .GroupBy(s => s.LoanId)
            .Select(g => new { LoanId = g.Key, Overdue = g.Sum(s => (s.Principal ?? 0m) - (s.PrincipalPaid ?? 0m) + (s.Interest ?? 0m) - (s.InterestPaid ?? 0m)) })
            .ToDictionaryAsync(x => x.LoanId ?? 0, x => x.Overdue, cancellationToken);

        var perClient = await _db.Loans
            .Where(l => loanIds.Contains(l.Id))
            .GroupBy(l => l.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                ActiveLoans = g.Count(l => l.Status == LoanStatus.Disbursed),
                TotalDisbursed = g.Sum(l => l.ApprovedAmount ?? 0m),
                LoanIds = g.Select(l => l.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        var clientIds = perClient.Where(p => p.ClientId.HasValue).Select(p => p.ClientId!.Value).ToList();
        var clients = await _db.Clients.Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var rows = perClient.Select(p =>
        {
            clients.TryGetValue(p.ClientId ?? 0, out var client);
            var overdue = p.LoanIds.Sum(id => overdueByLoan.GetValueOrDefault(id));
            return (IReadOnlyList<string?>)[client?.AccountNo, client?.FullName ?? client?.DisplayName, p.ActiveLoans.ToString(), p.TotalDisbursed.ToString("F2"), overdue.ToString("F2")];
        }).ToList();

        return new ReportResult(ScheduledReportName.IndividualIndicatorReport, ["Account No", "Client", "Active Loans", "Total Disbursed", "Current Overdue"], rows);
    }

    /// <summary>Per loan officer: active loan count, total disbursed, total outstanding, and portfolio-at-risk % (NPA loans / active loans).</summary>
    private async Task<ReportResult> LoanOfficerPerformanceAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loanIds = await FilteredLoans(filter).Select(l => l.Id).ToListAsync(cancellationToken);

        var perOfficer = await _db.Loans
            .Where(l => loanIds.Contains(l.Id))
            .GroupBy(l => l.LoanOfficerId)
            .Select(g => new
            {
                LoanOfficerId = g.Key,
                ActiveLoans = g.Count(l => l.Status == LoanStatus.Disbursed),
                TotalDisbursed = g.Sum(l => l.ApprovedAmount ?? 0m),
                NpaLoans = g.Count(l => l.Status == LoanStatus.Disbursed && l.IsNpa),
            })
            .ToListAsync(cancellationToken);

        var officerIds = perOfficer.Where(p => p.LoanOfficerId.HasValue).Select(p => p.LoanOfficerId!.Value).ToList();
        var officers = await _db.Users.Where(u => officerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        var rows = perOfficer.Select(p =>
        {
            officers.TryGetValue(p.LoanOfficerId ?? 0, out var officer);
            var par = p.ActiveLoans == 0 ? 0 : Math.Round((decimal)p.NpaLoans / p.ActiveLoans * 100m, 2);
            return (IReadOnlyList<string?>)
                [officer is null ? null : $"{officer.FirstName} {officer.LastName}", p.ActiveLoans.ToString(), p.TotalDisbursed.ToString("F2"), p.NpaLoans.ToString(), par.ToString("F2")];
        }).ToList();

        return new ReportResult(ScheduledReportName.LoanOfficerPerformanceReport, ["Loan Officer", "Active Loans", "Total Disbursed", "NPA Loans", "Portfolio At Risk %"], rows);
    }
}
