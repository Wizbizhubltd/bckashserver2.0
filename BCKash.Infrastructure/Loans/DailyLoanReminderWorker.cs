using BCKash.Application.Loans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Sends the customers' repayment reminders (<see cref="ILoanNotificationService.SendDueRemindersAsync"/>)
/// shortly after start-up and then daily. Each reminder goes once, so restarts or a manual
/// POST /loans/reminders/run-due never send it twice.
/// </summary>
public sealed class DailyLoanReminderWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyLoanReminderWorker> _logger;

    public DailyLoanReminderWorker(IServiceScopeFactory scopeFactory, ILogger<DailyLoanReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
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
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
