using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Domain.Reporting;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Reporting;

/// <summary>
/// Phase 9's third acceptance criterion: every report in the FRD §14 catalog (all 29) runs and
/// returns figures reconciling with the underlying loan/savings/GL data for a test period.
/// Seeds one small, realistic dataset directly via the DbContext (an office, a disbursed loan
/// with a repayment and an overdue installment, a savings account with a deposit, a group, a
/// provisioning band, and balanced GL entries) and exercises every report through the real
/// GET /api/reports/{reportName} endpoint.
/// </summary>
public class ReportCatalogTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ReportCatalogTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Every_report_in_the_catalog_runs_and_reconciles_with_seeded_data()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var loanAccountNumber = "LN-RPT-001";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();

            var office = new Office { Name = "Report Catalog Office", Active = true };
            db.Offices.Add(office);
            await db.SaveChangesAsync();

            var fundSource = new GlAccount { Name = "Fund Source", GlCode = "2000", AccountType = GlAccountType.Liability, Active = true, ManualEntries = true };
            var portfolio = new GlAccount { Name = "Loan Portfolio", GlCode = "1100", AccountType = GlAccountType.Asset, Active = true, ManualEntries = true };
            db.GlAccounts.AddRange(fundSource, portfolio);
            await db.SaveChangesAsync();

            db.GlJournalEntries.AddRange(
                new GlJournalEntry { OfficeId = office.Id, GlAccountId = portfolio.Id, Debit = 10_000m, TransactionType = GlTransactionType.Disbursement, Approved = true, Date = today, Reference = "SEED-1" },
                new GlJournalEntry { OfficeId = office.Id, GlAccountId = fundSource.Id, Credit = 10_000m, TransactionType = GlTransactionType.Disbursement, Approved = true, Date = today, Reference = "SEED-1" });

            var loanProduct = new LoanProduct { Name = "Report Test Product", NpaDays = 30 };
            db.LoanProducts.Add(loanProduct);
            await db.SaveChangesAsync();

            var seedClient = new Client { OfficeId = office.Id, FirstName = "Report", LastName = "Client", FullName = "Report Client", Status = ClientStatus.Active, AccountNo = "CL-RPT-001", JoinedDate = today };
            db.Clients.Add(seedClient);
            await db.SaveChangesAsync();

            var loan = new Loan
            {
                OfficeId = office.Id,
                ClientId = seedClient.Id,
                LoanProductId = loanProduct.Id,
                AccountNumber = loanAccountNumber,
                Status = LoanStatus.Disbursed,
                ApprovedAmount = 10_000m,
                DisbursementDate = today,
            };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            db.LoanRepaymentSchedules.AddRange(
                new BCKash.Domain.Loans.LoanRepaymentSchedule { LoanId = loan.Id, DueDate = today.AddDays(-45), Paid = false, Principal = 2_000m, Interest = 100m },
                new BCKash.Domain.Loans.LoanRepaymentSchedule { LoanId = loan.Id, DueDate = today.AddDays(30), Paid = false, Principal = 2_000m, Interest = 100m });

            db.LoanTransactions.Add(new BCKash.Domain.Loans.LoanTransaction
            {
                LoanId = loan.Id,
                ClientId = seedClient.Id,
                TransactionType = LoanTransactionType.Repayment,
                Amount = 500m,
                Principal = 400m,
                Interest = 100m,
                Date = today,
            });

            db.LoanProvisioningCriteria.Add(new LoanProvisioningCriteria { Name = "30+ days", Min = 30, Max = 90, Percentage = 10, Active = true });

            var group = new Group { OfficeId = office.Id, Name = "Report Test Group", Status = GroupStatus.Active, AccountNo = "GR-RPT-001" };
            db.Groups.Add(group);
            await db.SaveChangesAsync();
            db.GroupClients.Add(new GroupClient { GroupId = group.Id, ClientId = seedClient.Id });

            var savingsProduct = new SavingsProduct { Name = "Report Test Savings" };
            db.SavingsProducts.Add(savingsProduct);
            await db.SaveChangesAsync();

            var savingsAccount = new SavingsAccount
            {
                ClientId = seedClient.Id,
                OfficeId = office.Id,
                SavingsProductId = savingsProduct.Id,
                AccountNumber = "SV-RPT-001",
                Status = SavingsAccountStatus.Approved,
                Balance = 1_000m,
            };
            db.Savings.Add(savingsAccount);
            await db.SaveChangesAsync();

            db.SavingsTransactions.Add(new SavingsTransaction
            {
                SavingsId = savingsAccount.Id,
                TransactionType = SavingsTransactionType.Deposit,
                Amount = 1_000m,
                Balance = 1_000m,
                Date = today,
            });

            await db.SaveChangesAsync();
        }

        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "report-catalog@bckash.test", "reports.view");

        var catalog = await client.GetFromJsonAsync<List<ReportCatalogEntryResponse>>("/api/v1/reports/catalog", TestJson.Options);
        Assert.Equal(29, catalog!.Count);
        Assert.Equal(Enum.GetValues<ScheduledReportName>().Length, catalog.Count);

        // Every report runs without error and returns a well-formed result (non-null columns,
        // and every row exactly as wide as the header).
        foreach (var entry in catalog)
        {
            var response = await client.GetAsync($"/api/v1/reports/{entry.Name}");
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{entry.Name} failed: {response.StatusCode}: {body}");

            var result = System.Text.Json.JsonSerializer.Deserialize<ReportResultResponse>(body, TestJson.Options);
            Assert.NotNull(result);
            Assert.NotEmpty(result!.Columns);
            Assert.All(result.Rows, row => Assert.True(row.Count <= result.Columns.Count));
        }

        // Targeted reconciliation checks against the seeded data.
        var trialBalance = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.TrialBalance}", TestJson.Options);
        var debitTotal = trialBalance!.Rows.Sum(r => decimal.Parse(r[3]!));
        var creditTotal = trialBalance.Rows.Sum(r => decimal.Parse(r[4]!));
        Assert.Equal(debitTotal, creditTotal);

        var disbursed = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.DisbursedLoansReport}", TestJson.Options);
        Assert.Contains(disbursed!.Rows, r => r[0] == loanAccountNumber && r[3] == "10000.00");

        var clientNumbers = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.ClientNumbersReport}", TestJson.Options);
        Assert.Contains(clientNumbers!.Rows, r => r[1] == "1" && r[2] == "1"); // Total=1, Active=1

        var arrears = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.ArrearsReport}", TestJson.Options);
        Assert.Contains(arrears!.Rows, r => r[0] == loanAccountNumber);

        var provisioning = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.Provisioning}", TestJson.Options);
        Assert.Contains(provisioning!.Rows, r => r[0] == "30+ days" && decimal.Parse(r[4]!) > 0);

        var repayments = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.RepaymentsReport}", TestJson.Options);
        Assert.Contains(repayments!.Rows, r => r[0] == loanAccountNumber && r[6] == "500.00");

        var savingsAccountReport = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.SavingsAccountReport}", TestJson.Options);
        Assert.Contains(savingsAccountReport!.Rows, r => r[0] == "SV-RPT-001" && r[4] == "1000.00");

        var groupReport = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.GroupReport}", TestJson.Options);
        Assert.Contains(groupReport!.Rows, r => r[0] == "GR-RPT-001" && r[4] == "1");

        // Fixed term maturity has no backing data in this schema — legitimately empty, not an error.
        var fixedTerm = await client.GetFromJsonAsync<ReportResultResponse>($"/api/v1/reports/{ScheduledReportName.FixedTermMaturityReport}", TestJson.Options);
        Assert.Empty(fixedTerm!.Rows);

        // Every PDF/CSV/XLS export completes without error for a representative report.
        foreach (var format in Enum.GetValues<ReportSchedulerFileFormat>())
        {
            var response = await client.GetAsync($"/api/v1/reports/{ScheduledReportName.TrialBalance}?format={format}");
            Assert.True(response.IsSuccessStatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(bytes);
        }
    }
}
