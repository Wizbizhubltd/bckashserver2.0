using BCKash.Application.Loans;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Sends the customers' repayment reminders (<see cref="ILoanNotificationService.SendDueRemindersAsync"/>)
/// daily at a fixed time in Nigeria (Jobs:ReminderRunTime, default 07:00 — customers get them in the morning, before the office portal's busy hours). Each reminder goes once, so restarts or a manual
/// POST /loans/reminders/run-due never send it twice.
/// </summary>
public sealed class DailyLoanReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyLoanReminderWorker> _logger;
    private readonly TimeOnly _runAt;

    public DailyLoanReminderWorker(IServiceScopeFactory scopeFactory, ILogger<DailyLoanReminderWorker> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Jobs:ReminderRunTime ("HH:mm", Nigerian time).
        _runAt = DailyRunSchedule.Parse(configuration["Jobs:ReminderRunTime"], new TimeOnly(07, 0));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(DailyRunSchedule.Until(_runAt, DateTime.UtcNow), stoppingToken);
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    // Close out any loan already fully repaid (e.g. repaid before loans closed automatically) so it isn't reminded.
                    await scope.ServiceProvider.GetRequiredService<ILoanCompletionService>().SweepAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<ILoanNotificationService>().SendDueRemindersAsync(cancellationToken: stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Never take the API down over this — the next run catches up (see LoanNotificationKeys.CatchUpDays).
                    _logger.LogError(ex, "Loan reminder run failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
