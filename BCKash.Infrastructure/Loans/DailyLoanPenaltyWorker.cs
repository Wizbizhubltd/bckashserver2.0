using BCKash.Application.Loans;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Runs <see cref="ILoanPenaltyService.RunDueAsync"/> daily at a fixed time in Nigeria (Jobs:PenaltyRunTime, default 01:00 — overnight, out of office hours). The run
/// is idempotent and skips itself while automatic penalties are off, so restarts, overlapping
/// instances or a manual POST /loans/penalties/run-due never double-charge.
/// </summary>
public sealed class DailyLoanPenaltyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyLoanPenaltyWorker> _logger;
    private readonly TimeOnly _runAt;

    public DailyLoanPenaltyWorker(IServiceScopeFactory scopeFactory, ILogger<DailyLoanPenaltyWorker> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Jobs:PenaltyRunTime ("HH:mm", Nigerian time).
        _runAt = DailyRunSchedule.Parse(configuration["Jobs:PenaltyRunTime"], new TimeOnly(01, 0));
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
                    await scope.ServiceProvider.GetRequiredService<ILoanPenaltyService>().RunDueAsync(cancellationToken: stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Never take the API down over this — the next run catches up (see LoanPenaltyCalculator.CatchUpDays).
                    _logger.LogError(ex, "Automatic penalty run failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
