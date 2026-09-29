using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>Settings → Loan → "Overdue &amp; penalty rules": thresholds and automatic penalties.</summary>
public class OverdueAndPenaltyRulesTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public OverdueAndPenaltyRulesTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(OverdueRuleKeys.RepaymentOverdueDays, "-1")]
    [InlineData(OverdueRuleKeys.LoanOverdueDays, "a week")]
    [InlineData(OverdueRuleKeys.AutoApplyPenalty, "yes")]
    public async Task Invalid_rule_values_are_rejected(string key, string value)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"rules-invalid-{key}@bckash.test", "settings.manage");

        var response = await client.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(key, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_instalment_only_counts_as_late_once_the_threshold_has_passed()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "rules-late@bckash.test", "loan-applications.manage");
        await SetAsync(OverdueRuleKeys.RepaymentOverdueDays, "0");
        var loanId = await SeedLoanAsync("THRESHOLD", Instalment(Today.AddDays(-2), 1000, 100));

        Assert.Contains(loanId, await LateLoanIdsAsync(staff));

        await SetAsync(OverdueRuleKeys.RepaymentOverdueDays, "3");
        Assert.DoesNotContain(loanId, await LateLoanIdsAsync(staff));
    }

    [Fact]
    public async Task Due_penalties_are_charged_once_onto_the_missed_instalment_and_collected_with_it()
    {
        var servicing = await AuthenticatedClientFactory.CreateAsync(_factory, "rules-penalty@bckash.test", "loan-servicing.manage");
        await SetAsync(OverdueRuleKeys.RepaymentOverdueDays, "0");
        await SetAsync(OverdueRuleKeys.AutoApplyPenalty, "1");
        int chargeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var charge = new Charge
            {
                Name = "Late fee (test)",
                Product = ChargeProduct.Loan,
                ChargeType = ChargeType.OverdueInstallmentFee,
                ChargeOption = ChargeOption.InstallmentPrincipalInterestDue,
                Amount = 5,
                Penalty = true,
                GraceDays = 3,
                Active = true,
            };
            db.Charges.Add(charge);
            await db.SaveChangesAsync();
            chargeId = charge.Id;
        }

        // Due 5 days ago, 3 days' grace — charged. Due yesterday — still in grace, not charged.
        var loanId = await SeedLoanAsync("PENALTY", Instalment(Today.AddDays(-5), 1000, 100), Instalment(Today.AddDays(-1), 1000, 100));

        var first = await (await servicing.PostAsync("/api/v1/loans/penalties/run-due", null)).Content.ReadFromJsonAsync<PenaltyRunResult>(TestJson.Options);
        var second = await (await servicing.PostAsync("/api/v1/loans/penalties/run-due", null)).Content.ReadFromJsonAsync<PenaltyRunResult>(TestJson.Options);

        Assert.True(first!.PenaltiesCharged >= 1);
        Assert.Equal(0, second!.PenaltiesCharged);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var schedules = await assertDb.LoanRepaymentSchedules.Where(s => s.LoanId == loanId).OrderBy(s => s.DueDate).ToListAsync();
        Assert.Equal(55m, schedules[0].Penalty); // 5% of 1,100
        Assert.Null(schedules[1].Penalty);

        var loanCharge = await assertDb.LoanCharges.SingleAsync(c => c.LoanId == loanId && c.ChargeId == chargeId);
        Assert.True(loanCharge.Penalty);
        Assert.Equal(55m, loanCharge.Amount);
        Assert.Single(await assertDb.LoanTransactions.Where(t => t.LoanId == loanId && t.TransactionType == LoanTransactionType.OverdueInstallmentFee).ToListAsync());

        // Paying the missed instalment plus its penalty clears both.
        using var repayScope = _factory.Services.CreateScope();
        var repayment = await repayScope.ServiceProvider.GetRequiredService<ILoanRepaymentService>().RecordRepaymentAsync(loanId, 1155m, null, Today, null);
        Assert.Equal(LoanRepaymentWriteOutcome.Success, repayment.Outcome);

        using var paidScope = _factory.Services.CreateScope();
        var paid = await paidScope.ServiceProvider.GetRequiredService<BCKashDbContext>().LoanRepaymentSchedules
            .Where(s => s.LoanId == loanId).OrderBy(s => s.DueDate).FirstAsync();
        Assert.Equal(55m, paid.PenaltyPaid);
        Assert.True(paid.Paid);
    }

    [Fact]
    public async Task Nothing_is_charged_while_automatic_penalties_are_off()
    {
        await SetAsync(OverdueRuleKeys.AutoApplyPenalty, "0");
        using var scope = _factory.Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ILoanPenaltyService>().RunDueAsync();

        Assert.True(result.Skipped);
    }

    private static LoanRepaymentSchedule Instalment(DateOnly due, decimal principal, decimal interest) =>
        new() { DueDate = due, Principal = principal, Interest = interest, TotalDue = principal + interest };

    private async Task<int> SeedLoanAsync(string account, params LoanRepaymentSchedule[] instalments)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var loan = new Loan { Status = LoanStatus.Disbursed, Principal = 10000, AccountNumber = account, ExpectedMaturityDate = Today.AddMonths(6) };
        foreach (var instalment in instalments)
        {
            loan.LoanRepaymentSchedules.Add(instalment);
        }

        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan.Id;
    }

    private async Task SetAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting is null)
        {
            setting = new Setting { SettingKey = key };
            db.Settings.Add(setting);
        }

        setting.SettingValue = value;
        await db.SaveChangesAsync();
    }

    private static async Task<List<int>> LateLoanIdsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<LoanListItemResponse>>("/api/v1/loans/late?pageSize=100", TestJson.Options);
        return page!.Items.Select(i => i.Id).ToList();
    }
}
