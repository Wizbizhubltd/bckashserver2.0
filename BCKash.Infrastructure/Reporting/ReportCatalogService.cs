using BCKash.Application.GeneralLedger;
using BCKash.Application.Reporting;
using BCKash.Domain.Reporting;

namespace BCKash.Infrastructure.Reporting;

/// <summary>Single entry point over the full FRD §14 catalog — see the interface doc comment.</summary>
public class ReportCatalogService : IReportCatalogService
{
    private static readonly IReadOnlyDictionary<ScheduledReportName, ScheduledReportCategory> CategoryByName = new Dictionary<ScheduledReportName, ScheduledReportCategory>
    {
        [ScheduledReportName.ClientNumbersReport] = ScheduledReportCategory.ClientReport,
        [ScheduledReportName.ClientsOverview] = ScheduledReportCategory.ClientReport,
        [ScheduledReportName.TopClientsReport] = ScheduledReportCategory.ClientReport,
        [ScheduledReportName.DisbursedLoansReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.LoanPortfolioReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.ExpectedRepaymentsReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.RepaymentsReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.CollectionReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.ArrearsReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.LoanSizesReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.IndividualIndicatorReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.LoanOfficerPerformanceReport] = ScheduledReportCategory.LoanReport,
        [ScheduledReportName.BalanceSheet] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.TrialBalance] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.ProfitAndLoss] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.CashFlow] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.Provisioning] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.HistoricalIncomeStatement] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.JournalsReport] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.AccruedInterest] = ScheduledReportCategory.FinancialReport,
        [ScheduledReportName.GroupReport] = ScheduledReportCategory.GroupReport,
        [ScheduledReportName.GroupBreakdown] = ScheduledReportCategory.GroupReport,
        [ScheduledReportName.GroupIndicatorReport] = ScheduledReportCategory.GroupReport,
        [ScheduledReportName.SavingsAccountReport] = ScheduledReportCategory.SavingsReport,
        [ScheduledReportName.SavingsBalanceReport] = ScheduledReportCategory.SavingsReport,
        [ScheduledReportName.SavingsTransactionReport] = ScheduledReportCategory.SavingsReport,
        [ScheduledReportName.FixedTermMaturityReport] = ScheduledReportCategory.SavingsReport,
        [ScheduledReportName.ProductsSummary] = ScheduledReportCategory.OrganisationReport,
        [ScheduledReportName.AuditReport] = ScheduledReportCategory.OrganisationReport,
    };

    private readonly IClientReportService _clientReports;
    private readonly ILoanReportService _loanReports;
    private readonly IGlReportService _financialReports;
    private readonly IGroupReportService _groupReports;
    private readonly ISavingsReportService _savingsReports;
    private readonly IOrganisationReportService _organisationReports;

    public ReportCatalogService(
        IClientReportService clientReports, ILoanReportService loanReports, IGlReportService financialReports,
        IGroupReportService groupReports, ISavingsReportService savingsReports, IOrganisationReportService organisationReports)
    {
        _clientReports = clientReports;
        _loanReports = loanReports;
        _financialReports = financialReports;
        _groupReports = groupReports;
        _savingsReports = savingsReports;
        _organisationReports = organisationReports;
    }

    public IReadOnlyList<(ScheduledReportName Name, ScheduledReportCategory Category)> ListCatalog() =>
        CategoryByName.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Item2).ThenBy(x => x.Item1).ToList();

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default)
    {
        if (!CategoryByName.TryGetValue(reportName, out var category))
        {
            throw new ArgumentOutOfRangeException(nameof(reportName), reportName, "Unknown report name.");
        }

        return category switch
        {
            ScheduledReportCategory.ClientReport => _clientReports.RunAsync(reportName, filter, cancellationToken),
            ScheduledReportCategory.LoanReport => _loanReports.RunAsync(reportName, filter, cancellationToken),
            ScheduledReportCategory.FinancialReport => _financialReports.RunFinancialReportAsync(reportName, filter, cancellationToken),
            ScheduledReportCategory.GroupReport => _groupReports.RunAsync(reportName, filter, cancellationToken),
            ScheduledReportCategory.SavingsReport => _savingsReports.RunAsync(reportName, filter, cancellationToken),
            ScheduledReportCategory.OrganisationReport => _organisationReports.RunAsync(reportName, filter, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
        };
    }
}
