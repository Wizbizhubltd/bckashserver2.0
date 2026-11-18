using System.Threading.Channels;
using BCKash.Application.Auth;
using BCKash.Application.Communications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Auth;

internal static class OtpDelivery
{
    /// <summary>
    /// Email and SMS at the same time, each with its own scope (the senders are scoped and may use the
    /// DbContext), so the SMS — usually what the user is waiting for — doesn't queue behind SMTP.
    /// </summary>
    public static Task SendInParallelAsync(IServiceScopeFactory scopeFactory, ILogger logger, OtpMessage message, CancellationToken cancellationToken)
    {
        async Task SendOneAsync(OtpMessage part)
        {
            using var scope = scopeFactory.CreateScope();
            await SendAsync(
                scope.ServiceProvider.GetRequiredService<IEmailSender>(),
                scope.ServiceProvider.GetRequiredService<IOtpSmsSender>(),
                logger,
                part,
                cancellationToken);
        }

        var parts = new List<Task>();
        if (!string.IsNullOrWhiteSpace(message.Phone))
        {
            parts.Add(SendOneAsync(message with { Email = null }));
        }

        if (!string.IsNullOrWhiteSpace(message.Email))
        {
            parts.Add(SendOneAsync(message with { Phone = null }));
        }

        return Task.WhenAll(parts);
    }

    // Delivery is best-effort per channel: a failure on one (a bad Termii key, SMTP relay
    // hiccup, etc.) must not block the other from going out, and must never surface as a
    // request failure — the OTP row already exists, so the user can still receive/retry it.
    // Customers' loan messages (LoanNotificationService) go through here too, one channel per message.
    public static async Task SendAsync(IEmailSender emailSender, IOtpSmsSender smsSender, ILogger logger, OtpMessage message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.Email))
        {
            try
            {
                await emailSender.SendAsync(message.Email, message.Subject, message.Body, null, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to email \"{Subject}\" to {Email}", message.Subject, message.Email);
            }
        }

        if (!string.IsNullOrWhiteSpace(message.Phone))
        {
            try
            {
                await smsSender.SendAsync(message.Phone!, message.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send an SMS to {Phone}", message.Phone);
            }
        }
    }
}

/// <summary>
/// Production dispatcher: queues the message and returns immediately; background workers deliver it.
/// Two lanes: codes someone is waiting for (login, password reset, loan-application confirmation) get
/// several workers of their own, and bulk messages (daily customer reminders) get one. Before this,
/// a single worker delivered everything in order — so a reminder run of thousands of SMS left staff
/// waiting minutes for a login code, often past its expiry.
/// </summary>
public sealed class BackgroundOtpDispatcher : BackgroundService, IOtpDispatcher
{
    // Each worker handles one message at a time; a slow SMTP handshake or SMS call only holds up its own worker.
    private const int UrgentWorkers = 4;
    private const int BulkWorkers = 1;

    private readonly Channel<OtpMessage> _urgent = Channel.CreateUnbounded<OtpMessage>();
    private readonly Channel<OtpMessage> _bulk = Channel.CreateUnbounded<OtpMessage>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundOtpDispatcher> _logger;

    public BackgroundOtpDispatcher(IServiceScopeFactory scopeFactory, ILogger<BackgroundOtpDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task DispatchAsync(OtpMessage message, CancellationToken cancellationToken = default) =>
        (message.Bulk ? _bulk : _urgent).Writer.WriteAsync(message, cancellationToken).AsTask();

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            Enumerable.Range(0, UrgentWorkers).Select(_ => DrainAsync(_urgent.Reader, stoppingToken))
                .Concat(Enumerable.Range(0, BulkWorkers).Select(_ => DrainAsync(_bulk.Reader, stoppingToken))));

    private async Task DrainAsync(ChannelReader<OtpMessage> reader, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(stoppingToken))
            {
                await OtpDelivery.SendInParallelAsync(_scopeFactory, _logger, message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}

/// <summary>Test dispatcher: delivers before returning, so tests can read the code off the recording senders right after the request.</summary>
public sealed class InlineOtpDispatcher : IOtpDispatcher
{
    private readonly IEmailSender _emailSender;
    private readonly IOtpSmsSender _smsSender;
    private readonly ILogger<InlineOtpDispatcher> _logger;

    public InlineOtpDispatcher(IEmailSender emailSender, IOtpSmsSender smsSender, ILogger<InlineOtpDispatcher> logger)
    {
        _emailSender = emailSender;
        _smsSender = smsSender;
        _logger = logger;
    }

    public Task DispatchAsync(OtpMessage message, CancellationToken cancellationToken = default) =>
        OtpDelivery.SendAsync(_emailSender, _smsSender, _logger, message, cancellationToken);
}
