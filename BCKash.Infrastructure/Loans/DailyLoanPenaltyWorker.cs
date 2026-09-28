using BCKash.Application.Loans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Runs <see cref="ILoanPenaltyService.RunDueAsync"/> shortly after start-up and then daily. The run
/// is idempotent and skips itself while automatic penalties are off, so restarts, overlapping
/// instances or a manual POST /loans/penalties/run-due never double-charge.
/// </summary>
public sealed class DailyLoanPenaltyWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyLoanPenaltyWorker> _logger;

    public DailyLoanPenaltyWorker(IServiceScopeFactory scopeFactory, ILogger<DailyLoanPenaltyWorker> logger)
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
                    await scope.ServiceProvider.GetRequiredService<ILoanPenaltyService>().RunDueAsync(cancellationToken: stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Never take the API down over this — the next run catches up (see LoanPenaltyCalculator.CatchUpDays).
                    _logger.LogError(ex, "Automatic penalty run failed.");
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
