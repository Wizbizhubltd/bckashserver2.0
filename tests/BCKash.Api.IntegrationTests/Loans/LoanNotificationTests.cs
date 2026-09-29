using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Application.Loans;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>
/// The customer's loan messages behind Settings → Notifications. Its own fixture: switching messages
/// on changes what every test sharing the database sends.
/// </summary>
public class LoanNotificationTests : IClassFixture<BCKashWebApplicationFactory>
{
    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    private readonly BCKashWebApplicationFactory _factory;

    public LoanNotificationTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private RecordingOtpSmsSender Sms => _factory.Services.GetRequiredService<RecordingOtpSmsSender>();

    private RecordingEmailSender Emails => (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    [Fact]
    public async Task Loan_messages_go_only_once_switched_on_with_their_templates_filled_in()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "notify-staff@bckash.test", AllPermissions);
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "notify-sa@bckash.test");

        // Never switched on: the customer hears nothing.
        var (_, quietPhone, quietEmail) = await RunLoanAsync(staff, "Quiet");
        Assert.DoesNotContain(Sms.Sent, s => s.ToPhone == quietPhone);
        Assert.DoesNotContain(Emails.Sent, e => e.ToAddress == quietEmail);

        await SaveAsync(superAdmin, new()
        {
            ["loan_approved_auto_sms"] = "1",
            ["loan_approved_auto_email"] = "1",
            // The legacy templates wrote amounts as "${…}" and emails as HTML.
            ["loan_approved_sms_template"] = "Dear {clientName}, loan {loanNumber} is approved for ${approvedAmount}.",
            ["loan_approved_email_subject"] = "Approved: {loanNumber}",
            ["loan_approved_email_template"] = "<p>Dear {clientName},</p><p>Your loan is approved.</p>",
            ["loan_disbursed_auto_sms"] = "1",
            ["auto_payment_receipt_sms"] = "1",
        });

        var (loanNumber, phone, email) = await RunLoanAsync(staff, "Loud");

        var approvedSms = Sms.Sent.Single(s => s.ToPhone == phone && s.Message.Contains("approved"));
        Assert.Equal($"Dear Loud Client, loan {loanNumber} is approved for ₦12,000.", approvedSms.Message);

        var approvedEmail = Emails.Sent.Single(e => e.ToAddress == email);
        Assert.Equal($"Approved: {loanNumber}", approvedEmail.Subject);
        Assert.StartsWith("Dear Loud Client,\nYour loan is approved.", approvedEmail.Body);
        Assert.DoesNotContain("<p>", approvedEmail.Body);

        // No template saved: the default wording, with the first instalment filled in. It's a savings loan, so
        // what the customer pays is grossed up: 1,120 ÷ 0.975 = 1,148.72. The 1,120 repayment puts 28 into their
        // savings and 1,092 onto the loan, leaving 12,348 owed — 12,664.62 for the customer to pay.
        Assert.Contains(Sms.Sent, s => s.ToPhone == phone && s.Message.Contains("has been disbursed") && s.Message.Contains("₦1,148.72"));
        Assert.Contains(Sms.Sent, s => s.ToPhone == phone && s.Message.Contains("received your payment of ₦1,120") && s.Message.Contains("₦12,664.62"));

        // Only the approved message has email switched on.
        Assert.Single(Emails.Sent, e => e.ToAddress == email);
    }

    [Fact]
    public async Task Repayment_reminders_go_once_each_when_due()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "notify-reminders-sa@bckash.test");
        var servicing = await AuthenticatedClientFactory.CreateAsync(_factory, "notify-reminders@bckash.test", "loan-servicing.manage");
        await SaveAsync(superAdmin, new()
        {
            ["auto_repayment_sms_reminder"] = "1",
            ["auto_repayment_days"] = "3",
            ["auto_overdue_repayment_sms_reminder"] = "1",
            ["auto_overdue_loan_sms_reminder"] = "1",
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        const string phone = "+2348031234599";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = new Client { FirstName = "Remi", LastName = "Nder", Mobile = phone, Status = ClientStatus.Active, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();

            // A loan whose last instalment fell due yesterday — overdue, with one instalment missed.
            var overdue = new Loan { ClientId = client.Id, ClientType = LoanClientType.Client, Status = LoanStatus.Disbursed, AccountNumber = "LNREMIND01", ApprovedAmount = 2000 };
            // A loan with an instalment due in two days.
            var upcoming = new Loan { ClientId = client.Id, ClientType = LoanClientType.Client, Status = LoanStatus.Disbursed, AccountNumber = "LNREMIND02", ApprovedAmount = 1000 };
            db.Loans.AddRange(overdue, upcoming);
            await db.SaveChangesAsync();

            db.LoanRepaymentSchedules.AddRange(
                new LoanRepaymentSchedule { LoanId = overdue.Id, Installment = 1, DueDate = today.AddDays(-30), Principal = 1000, PrincipalPaid = 1000, Interest = 0, TotalDue = 1000, Paid = true },
                new LoanRepaymentSchedule { LoanId = overdue.Id, Installment = 2, DueDate = today.AddDays(-1), Principal = 1000, Interest = 50, TotalDue = 1050 },
                new LoanRepaymentSchedule { LoanId = upcoming.Id, Installment = 1, DueDate = today.AddDays(2), Principal = 1000, Interest = 25, TotalDue = 1025 },
                // Paid off already: no reminder.
                new LoanRepaymentSchedule { LoanId = upcoming.Id, Installment = 2, DueDate = today.AddDays(1), Principal = 500, PrincipalPaid = 500, TotalDue = 500, Paid = true });
            await db.SaveChangesAsync();
        }

        // Other tests' loans share this database and may be due too, so check this customer's messages.
        var first = await (await servicing.PostAsync("/api/v1/loans/reminders/run-due", content: null)).Content.ReadFromJsonAsync<LoanReminderRunResult>(TestJson.Options);
        Assert.True(first!.UpcomingRepayments >= 1 && first.MissedRepayments >= 1 && first.OverdueLoans >= 1, first.ToString());

        var texts = Sms.Sent.Where(s => s.ToPhone == phone).Select(s => s.Message).ToList();
        Assert.Equal(3, texts.Count);
        Assert.Contains(texts, t => t.Contains("upcoming payment of ₦1,025") && t.Contains("LNREMIND02"));
        Assert.Contains(texts, t => t.Contains("missed a payment of ₦1,050") && t.Contains("LNREMIND01"));
        Assert.Contains(texts, t => t.Contains("LNREMIND01 is overdue"));

        var second = await (await servicing.PostAsync("/api/v1/loans/reminders/run-due", content: null)).Content.ReadFromJsonAsync<LoanReminderRunResult>(TestJson.Options);
        Assert.Equal(new LoanReminderRunResult(0, 0, 0), second);
        Assert.Equal(texts.Count, Sms.Sent.Count(s => s.ToPhone == phone));
    }

    /// <summary>Raises, approves, disburses and takes the first repayment on a 12,000 loan (12 × 1,120) for a client with a phone and email.</summary>
    private async Task<(string LoanNumber, string Phone, string Email)> RunLoanAsync(HttpClient staff, string label)
    {
        var productId = (await (await staff.PostAsJsonAsync("/api/v1/loan-products", ProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = (await (await staff.PostAsJsonAsync("/api/v1/clients", new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options))!.Id;

        var phone = $"+234803{Math.Abs(label.GetHashCode()) % 10_000_000:D7}";
        var email = $"notify-{label.ToLowerInvariant()}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = await db.Clients.SingleAsync(c => c.Id == clientId);
            (client.Mobile, client.Email) = (phone, email);
            await db.SaveChangesAsync();
        }

        var application = await (await staff.PostAsJsonAsync("/api/v1/loan-applications",
            new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approved = await (await staff.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;
        Assert.True((await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null))).StatusCode);

        var loan = await staff.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options);
        return (loan!.AccountNumber!, phone, email);
    }

    private static async Task SaveAsync(HttpClient superAdmin, Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            var response = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(key, value));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    // P=12000, 1%/month, 12 instalments, flat/equal-instalment: every instalment is 1,000 principal + 120 interest.
    private static SaveLoanProductRequest ProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
        MinimumPrincipal: 1000, DefaultPrincipal: 12000, MaximumPrincipal: 20000,
        MinimumLoanTerm: 6, DefaultLoanTerm: 12, MaximumLoanTerm: 24,
        RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
        MinimumInterestRate: 1, DefaultInterestRate: 1, MaximumInterestRate: 1, InterestRateType: InterestRateFrequencyType.Month,
        GraceOnInterestCharged: null, GraceOnPrincipal: null, GraceOnInterestPayment: null,
        AllowCustomGrace: false, AllowStandingInstructions: false,
        InterestMethod: LoanInterestMethod.Flat, AmortizationMethod: LoanAmortizationMethod.EqualInstallment,
        InterestCalculationPeriodType: InterestCalculationPeriodType.Same, YearDays: YearDaysType.Days365, MonthDays: MonthDaysType.Days30,
        LoanTransactionStrategy: LoanTransactionStrategy.InterestPrincipalPenaltyFees,
        IncludeInCycle: false, LockGuarantee: false, AllocateOverpayments: false, AllowAdditionalCharges: false,
        AccountingRule: LoanAccountingRule.Cash, NpaDays: 90, ArrearsGraceDays: null, NpaSuspendIncome: true,
        GlAccountFundSourceId: null, GlAccountLoanPortfolioId: null, GlAccountReceivableInterestId: null, GlAccountReceivableFeeId: null,
        GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null, GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null,
        GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);
}
