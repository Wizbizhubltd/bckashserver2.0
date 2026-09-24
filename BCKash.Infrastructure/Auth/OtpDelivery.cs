using System.Threading.Channels;
using BCKash.Application.Auth;
using BCKash.Application.Communications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Auth;

internal static class OtpDelivery
{
    // Delivery is best-effort per channel: a failure on one (a bad Termii key, SMTP relay
    // hiccup, etc.) must not block the other from going out, and must never surface as a
    // request failure — the OTP row already exists, so the user can still receive/retry it.
    public static async Task SendAsync(IEmailSender emailSender, IOtpSmsSender smsSender, ILogger logger, OtpMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message.Email, message.Subject, message.Body, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to email OTP to {Email}", message.Email);
        }

        if (!string.IsNullOrWhiteSpace(message.Phone))
        {
            try
            {
                await smsSender.SendAsync(message.Phone!, message.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to SMS OTP to {Phone}", message.Phone);
            }
        }
    }
}

/// <summary>Production dispatcher: queues the message and returns immediately; a background worker delivers it.</summary>
public sealed class BackgroundOtpDispatcher : BackgroundService, IOtpDispatcher
{
    private readonly Channel<OtpMessage> _queue = Channel.CreateUnbounded<OtpMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundOtpDispatcher> _logger;

    public BackgroundOtpDispatcher(IServiceScopeFactory scopeFactory, ILogger<BackgroundOtpDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task DispatchAsync(OtpMessage message, CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(message, cancellationToken).AsTask();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // The senders are scoped (SmtpEmailSender, typed HttpClients), so resolve per message.
            using var scope = _scopeFactory.CreateScope();
            await OtpDelivery.SendAsync(
                scope.ServiceProvider.GetRequiredService<IEmailSender>(),
                scope.ServiceProvider.GetRequiredService<IOtpSmsSender>(),
                _logger,
                message,
                stoppingToken);
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
