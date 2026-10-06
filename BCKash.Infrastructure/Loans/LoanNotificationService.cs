using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BCKash.Application.Auth;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>See <see cref="ILoanNotificationService"/>.</summary>
public partial class LoanNotificationService : ILoanNotificationService
{
    // Loans with a live repayment schedule. A List so EF can translate Contains.
    private static readonly List<LoanStatus> ActiveStatuses = [LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled];

    private readonly BCKashDbContext _db;
    private readonly IOtpDispatcher _dispatcher;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrencyDisplayProvider _currency;
    private readonly IOverdueRulesProvider _overdueRules;
    private readonly ILogger<LoanNotificationService> _logger;

    public LoanNotificationService(
        BCKashDbContext db,
        IOtpDispatcher dispatcher,
        ICompanyProfileProvider companyProfile,
        ICurrencyDisplayProvider currency,
        IOverdueRulesProvider overdueRules,
        ILogger<LoanNotificationService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _companyProfile = companyProfile;
        _currency = currency;
        _overdueRules = overdueRules;
        _logger = logger;
    }

    public Task LoanApprovedAsync(int loanId, CancellationToken cancellationToken = default) =>
        SafelyAsync(LoanNotificationKind.LoanApproved, loanId, async () =>
        {
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
            return loan is null ? null : await ValuesAsync(loan, null, cancellationToken);
        }, cancellationToken);

    public Task LoanDisbursedAsync(int loanId, CancellationToken cancellationToken = default) =>
        SafelyAsync(LoanNotificationKind.LoanDisbursed, loanId, async () =>
        {
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
            return loan is null ? null : await ValuesAsync(loan, null, cancellationToken);
        }, cancellationToken);

    public Task PaymentReceivedAsync(int loanId, decimal amount, DateOnly date, CancellationToken cancellationToken = default) =>
        SafelyAsync(LoanNotificationKind.PaymentReceived, loanId, async () =>
        {
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
            return loan is null ? null : await ValuesAsync(loan, (amount, date), cancellationToken);
        }, cancellationToken);

    public async Task<LoanReminderRunResult> SendDueRemindersAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var settings = await SettingsAsync(cancellationToken);
        var rules = await _overdueRules.GetAsync(cancellationToken);

        var upcoming = 0;
        if (IsWanted(settings, LoanNotificationKind.UpcomingRepayment))
        {
            var days = int.TryParse(settings.GetValueOrDefault(LoanNotificationKeys.ReminderDaysKey), out var d) && d is >= 0 and <= 365 ? d : LoanNotificationKeys.DefaultReminderDays;
            upcoming = await RemindInstalmentsAsync(
                LoanNotificationKind.UpcomingRepayment, LoanNotification.UpcomingRepaymentKind,
                from: today.AddDays(1), to: today.AddDays(days), cancellationToken);
        }

        var missed = 0;
        if (IsWanted(settings, LoanNotificationKind.MissedRepayment))
        {
            // Overdue once "Repayment overdue after N days" has passed — the same rule as the Late Loans list.
            var lastOverdue = today.AddDays(-rules.RepaymentOverdueDays - 1);
            missed = await RemindInstalmentsAsync(
                LoanNotificationKind.MissedRepayment, LoanNotification.MissedRepaymentKind,
                from: lastOverdue.AddDays(-LoanNotificationKeys.CatchUpDays + 1), to: lastOverdue, cancellationToken);
        }

        var overdue = 0;
        if (IsWanted(settings, LoanNotificationKind.LoanOverdue))
        {
            overdue = await RemindOverdueLoansAsync(today.AddDays(-rules.LoanOverdueDays - 1), cancellationToken);
        }

        return new LoanReminderRunResult(upcoming, missed, overdue);
    }

    /// <summary>Instalments due between <paramref name="from"/> and <paramref name="to"/> with money still owed, not yet reminded about.</summary>
    /// <remarks>
    /// The reminder runs read untracked and skip the Redis second-level cache (NotCacheable), and
    /// empty the change tracker after each message, so a run over thousands of loans holds one
    /// loan's rows at a time instead of all of them.
    /// </remarks>
    private async Task<int> RemindInstalmentsAsync(LoanNotificationKind kind, string logKind, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var reminded = _db.LoanNotifications.Where(n => n.Kind == logKind && n.ScheduleId != null).Select(n => n.ScheduleId!.Value);
        var instalments = await _db.LoanRepaymentSchedules
            .AsNoTracking()
            .NotCacheable()
            .Where(s => s.DueDate >= from && s.DueDate <= to && s.LoanId != null && ActiveStatuses.Contains(s.Loan!.Status) && !reminded.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var instalment in instalments.Where(s => Outstanding(s) > 0))
        {
            var loan = await _db.Loans.AsNoTracking().NotCacheable().FirstAsync(l => l.Id == instalment.LoanId, cancellationToken);
            // What the client pays — grossed up on a savings loan, so the instalment is still covered after their savings share.
            var values = await ValuesAsync(loan, (ClientSavingsRules.GrossUp(Outstanding(instalment), loan.SavingsRate), instalment.DueDate!.Value), cancellationToken);
            await SafelyAsync(kind, loan.Id, () => Task.FromResult<MessageData?>(values), cancellationToken);

            _db.LoanNotifications.Add(new LoanNotification { LoanId = loan.Id, ScheduleId = instalment.Id, Kind = logKind, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            sent++;
        }

        return sent;
    }

    /// <summary>Loans whose last instalment fell due on or before <paramref name="lastOverdue"/> (within the catch-up window), still owing, not yet told.</summary>
    private async Task<int> RemindOverdueLoansAsync(DateOnly lastOverdue, CancellationToken cancellationToken)
    {
        var earliest = lastOverdue.AddDays(-LoanNotificationKeys.CatchUpDays + 1);
        var told = _db.LoanNotifications.Where(n => n.Kind == LoanNotification.LoanOverdueKind).Select(n => n.LoanId);
        var loanIds = await _db.LoanRepaymentSchedules
            .NotCacheable()
            .Where(s => s.LoanId != null && ActiveStatuses.Contains(s.Loan!.Status))
            .GroupBy(s => s.LoanId!.Value)
            .Select(g => new { LoanId = g.Key, Maturity = g.Max(s => s.DueDate) })
            .Where(x => x.Maturity >= earliest && x.Maturity <= lastOverdue && !told.Contains(x.LoanId))
            .Select(x => x.LoanId)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var loanId in loanIds)
        {
            var loan = await _db.Loans.AsNoTracking().NotCacheable().FirstAsync(l => l.Id == loanId, cancellationToken);
            var values = await ValuesAsync(loan, null, cancellationToken);
            if (values.Balance <= 0)
            {
                continue;
            }

            await SafelyAsync(LoanNotificationKind.LoanOverdue, loanId, () => Task.FromResult<MessageData?>(values), cancellationToken);
            _db.LoanNotifications.Add(new LoanNotification { LoanId = loanId, Kind = LoanNotification.LoanOverdueKind, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            sent++;
        }

        return sent;
    }

    /// <summary>Builds and queues one message; a failure is logged, never thrown — the action behind it has already happened.</summary>
    private async Task SafelyAsync(LoanNotificationKind kind, int loanId, Func<Task<MessageData?>> data, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await SettingsAsync(cancellationToken);
            if (!IsWanted(settings, kind))
            {
                return;
            }

            var filled = await data();
            if (filled is null)
            {
                return;
            }

            var keys = LoanNotificationKeys.For[kind];
            var company = await _companyProfile.GetAsync(cancellationToken);

            if (IsOn(settings, keys.EmailSwitch) && !string.IsNullOrWhiteSpace(filled.Email))
            {
                var subject = Fill(Template(settings, keys.EmailSubject, keys.DefaultSubject), filled.Placeholders);
                var body = ToPlainText(Fill(Template(settings, keys.EmailTemplate, keys.DefaultMessage), filled.Placeholders)) + company.EmailFooter;
                await _dispatcher.DispatchAsync(new OtpMessage(filled.Email.Trim(), null, subject, body), cancellationToken);
            }

            if (IsOn(settings, keys.SmsSwitch) && !string.IsNullOrWhiteSpace(filled.Phone))
            {
                var text = ToPlainText(Fill(Template(settings, keys.SmsTemplate, keys.DefaultMessage), filled.Placeholders));
                await _dispatcher.DispatchAsync(new OtpMessage(null, filled.Phone.Trim(), string.Empty, text), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Couldn't send the {Kind} message for loan {LoanId}.", kind, loanId);
        }
    }

    /// <summary>Who a message goes to, what the loan still owes, and every placeholder a template may use.</summary>
    private sealed record MessageData(string? Email, string? Phone, decimal Balance, IReadOnlyDictionary<string, string> Placeholders);

    /// <summary>The message data for <paramref name="loan"/>; <paramref name="payment"/> is the amount and date the message is about.</summary>
    private async Task<MessageData> ValuesAsync(Loan loan, (decimal Amount, DateOnly Date)? payment, CancellationToken cancellationToken)
    {
        var currency = await _currency.GetAsync(cancellationToken);
        var company = await _companyProfile.GetAsync(cancellationToken);

        string? name = null, phone = null, email = null;
        if (loan.ClientId is { } clientId)
        {
            var client = await _db.Clients.Where(c => c.Id == clientId).Select(c => new { c.FirstName, c.LastName, c.DisplayName, c.Mobile, c.Phone, c.Email }).FirstOrDefaultAsync(cancellationToken);
            name = $"{client?.FirstName} {client?.LastName}".Trim() is { Length: > 0 } fullName ? fullName : client?.DisplayName;
            (phone, email) = (client?.Mobile ?? client?.Phone, client?.Email);
        }
        else if (loan.GroupId is { } groupId)
        {
            var group = await _db.Groups.Where(g => g.Id == groupId).Select(g => new { g.Name, g.Mobile, g.Phone, g.Email }).FirstOrDefaultAsync(cancellationToken);
            (name, phone, email) = (group?.Name, group?.Mobile ?? group?.Phone, group?.Email);
        }

        // Read-only, and every caller has already saved — untracked and uncached, since the schedule changes with every repayment.
        var schedule = await _db.LoanRepaymentSchedules.AsNoTracking().NotCacheable().Where(s => s.LoanId == loan.Id).OrderBy(s => s.DueDate).ThenBy(s => s.Installment).ToListAsync(cancellationToken);
        var balance = schedule.Count > 0 ? schedule.Sum(Outstanding) : loan.ApprovedAmount ?? loan.AppliedAmount ?? 0;
        var first = schedule.FirstOrDefault();
        var approved = loan.ApprovedAmount ?? loan.AppliedAmount ?? 0;

        return new MessageData(email, phone, balance, new Dictionary<string, string>
        {
            ["clientName"] = name ?? "Customer",
            ["loanNumber"] = loan.AccountNumber ?? $"#{loan.Id}",
            ["approvedAmount"] = currency.Format(approved),
            ["loanAmount"] = currency.Format(approved),
            ["loanBalance"] = currency.Format(ClientSavingsRules.GrossUp(balance, loan.SavingsRate)),
            ["paymentAmount"] = payment is { } p ? currency.Format(p.Amount) : string.Empty,
            ["paymentDate"] = payment is { } q ? FormatDate(q.Date) : string.Empty,
            ["firstPaymentAmount"] = first is null ? string.Empty : currency.Format(ClientSavingsRules.GrossUp(first.TotalDue ?? Outstanding(first), loan.SavingsRate)),
            ["firstPaymentDate"] = first?.DueDate is { } due ? FormatDate(due) : string.Empty,
            ["companyName"] = company.Name,
        });
    }

    /// <summary>What's still owed on an instalment: everything due, less what's been paid, waived or written off.</summary>
    private static decimal Outstanding(LoanRepaymentSchedule s) =>
        Math.Max(0,
            (s.Principal ?? 0) + (s.Interest ?? 0) + (s.Fees ?? 0) + (s.Penalty ?? 0)
            - (s.PrincipalPaid ?? 0) - (s.InterestPaid ?? 0) - (s.FeesPaid ?? 0) - (s.PenaltyPaid ?? 0)
            - (s.PrincipalWaived ?? 0) - (s.InterestWaived ?? 0) - (s.FeesWaived ?? 0) - (s.PenaltyWaived ?? 0)
            - (s.PrincipalWrittenOff ?? 0) - (s.InterestWrittenOff ?? 0) - (s.FeesWrittenOff ?? 0) - (s.PenaltyWrittenOff ?? 0));

    private static string FormatDate(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Replaces {placeholders}. The legacy templates wrote some as "${paymentAmount}" — the "$" is dropped,
    /// since the amounts already carry the configured currency symbol. Unknown placeholders are left as they are.
    /// </summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        PlaceholderPattern().Replace(template, m => values.TryGetValue(m.Groups["name"].Value, out var value) ? value : m.Value);

    /// <summary>Emails go as plain text, and the legacy templates are HTML — keep the words and line breaks, drop the tags.</summary>
    private static string ToPlainText(string text)
    {
        var withBreaks = LineBreakPattern().Replace(text, "\n");
        var stripped = WebUtility.HtmlDecode(TagPattern().Replace(withBreaks, string.Empty));
        return BlankLinesPattern().Replace(stripped, "\n\n").Trim();
    }

    private static string Template(IReadOnlyDictionary<string, string?> settings, string key, string fallback) =>
        settings.GetValueOrDefault(key) is { } value && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static bool IsOn(IReadOnlyDictionary<string, string?> settings, string key) =>
        settings.GetValueOrDefault(key)?.Trim().ToLowerInvariant() is "1" or "true";

    private static bool IsWanted(IReadOnlyDictionary<string, string?> settings, LoanNotificationKind kind) =>
        IsOn(settings, LoanNotificationKeys.For[kind].EmailSwitch) || IsOn(settings, LoanNotificationKeys.For[kind].SmsSwitch);

    /// <summary>Every notification setting, newest row per key.</summary>
    private async Task<Dictionary<string, string?>> SettingsAsync(CancellationToken cancellationToken)
    {
        var keys = LoanNotificationKeys.For.Values
            .SelectMany(k => new[] { k.EmailSwitch, k.SmsSwitch, k.EmailSubject, k.EmailTemplate, k.SmsTemplate })
            .Append(LoanNotificationKeys.ReminderDaysKey)
            .ToList();
        var rows = await _db.Settings.Where(s => keys.Contains(s.SettingKey)).Select(s => new { s.Id, s.SettingKey, s.SettingValue }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.SettingKey).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().SettingValue);
    }

    [GeneratedRegex(@"\$?\{(?<name>[A-Za-z]+)\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*p\s*>|</\s*div\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\n\s*\n\s*(\n\s*)+")]
    private static partial Regex BlankLinesPattern();
}
