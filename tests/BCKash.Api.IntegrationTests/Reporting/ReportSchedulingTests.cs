using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Communications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Reporting;

/// <summary>
/// Phase 9's fourth acceptance criterion: scheduled reports deliver by email in all three
/// supported formats. No real SMTP delivery happens in tests — <see cref="RecordingEmailSender"/>
/// is registered in place of <see cref="BCKash.Infrastructure.Communications.SmtpEmailSender"/>
/// whenever Testing:UseSqlite is set (see its own doc comment), so this asserts against what
/// would have been sent rather than an actual mailbox.
/// </summary>
public class ReportSchedulingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ReportSchedulingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(ReportSchedulerFileFormat.Pdf)]
    [InlineData(ReportSchedulerFileFormat.Csv)]
    [InlineData(ReportSchedulerFileFormat.Xls)]
    public async Task Running_a_schedule_emails_the_report_in_the_configured_format(ReportSchedulerFileFormat format)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"report-schedule-{format}@bckash.test", "report-schedules.manage");
        var recordingSender = (RecordingEmailSender)_factory.Services.GetRequiredService<BCKash.Application.Communications.IEmailSender>();
        var beforeCount = recordingSender.Sent.Count;

        var recipientAddress = $"finance-{format}@bckash.test";
        var created = await (await client.PostAsJsonAsync("/api/v1/report-schedules", new SaveReportScheduleRequest(
            Description: $"Trial balance ({format})", ReportStartDate: null, ReportStartTime: null,
            RecurrenceType: null, RecurFrequency: null, RecurInterval: null,
            EmailRecipients: recipientAddress, EmailSubject: "Trial Balance", EmailMessage: "See attached.",
            EmailAttachmentFileFormat: format, ReportCategory: ScheduledReportCategory.FinancialReport,
            ReportName: ScheduledReportName.TrialBalance, StartDate: null, EndDate: null,
            OfficeId: null, LoanOfficerId: null, LoanStatus: null, LoanProductId: null, Active: true)))
            .Content.ReadFromJsonAsync<ReportScheduleResponse>(TestJson.Options);

        var runResponse = await client.PostAsync($"/api/v1/report-schedules/{created!.Id}/run", content: null);
        Assert.True(runResponse.IsSuccessStatusCode);

        Assert.Equal(beforeCount + 1, recordingSender.Sent.Count);
        var sent = recordingSender.Sent[^1];
        Assert.Equal(recipientAddress, sent.ToAddress);
        Assert.Equal("Trial Balance", sent.Subject);
        Assert.NotNull(sent.Attachments);
        Assert.Single(sent.Attachments!);

        var attachment = sent.Attachments![0];
        Assert.NotEmpty(attachment.Content);
        Assert.Equal($"{ScheduledReportName.TrialBalance}.{ExpectedExtension(format)}", attachment.FileName);

        var afterRun = await client.GetFromJsonAsync<ReportScheduleResponse>($"/api/v1/report-schedules/{created.Id}", TestJson.Options);
        Assert.Equal(1, afterRun!.NumberOfRuns);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), afterRun.LastRunDate);
    }

    private static string ExpectedExtension(ReportSchedulerFileFormat format) => format switch
    {
        ReportSchedulerFileFormat.Pdf => "pdf",
        ReportSchedulerFileFormat.Csv => "csv",
        ReportSchedulerFileFormat.Xls => "xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
