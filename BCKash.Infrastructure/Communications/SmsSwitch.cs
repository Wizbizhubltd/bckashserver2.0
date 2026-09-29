using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Communications;

/// <summary>Reads the "SMS sending" master switch — see <see cref="ISmsSwitch"/>.</summary>
public class SmsSwitch : ISmsSwitch
{
    private readonly BCKashDbContext _db;

    public SmsSwitch(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsOnAsync(CancellationToken cancellationToken = default)
    {
        var saved = await _db.Settings
            .Where(s => s.SettingKey == ISmsSwitch.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => new { s.SettingValue })
            .FirstOrDefaultAsync(cancellationToken);

        // Only an explicit "off" stops SMS — a missing or unreadable value keeps messages flowing.
        return saved?.SettingValue?.Trim().ToLowerInvariant() is not ("0" or "false");
    }
}

/// <summary>Sends campaign SMS through the office's gateway only while SMS sending is switched on.</summary>
public class SwitchedSmsSender : ISmsSender
{
    private readonly ISmsSender _inner;
    private readonly ISmsSwitch _switch;
    private readonly ILogger<SwitchedSmsSender> _logger;

    public SwitchedSmsSender(ISmsSender inner, ISmsSwitch smsSwitch, ILogger<SwitchedSmsSender> logger)
    {
        _inner = inner;
        _switch = smsSwitch;
        _logger = logger;
    }

    public async Task SendAsync(SmsGateway gateway, string toPhone, string message, CancellationToken cancellationToken = default)
    {
        if (!await _switch.IsOnAsync(cancellationToken))
        {
            _logger.LogInformation("SMS sending is switched off — not sending to {Phone}.", toPhone);
            return;
        }

        await _inner.SendAsync(gateway, toPhone, message, cancellationToken);
    }
}

/// <summary>Sends sign-in and confirmation codes by SMS only while SMS sending is switched on; the email copy still goes.</summary>
public class SwitchedOtpSmsSender : IOtpSmsSender
{
    private readonly IOtpSmsSender _inner;
    private readonly ISmsSwitch _switch;
    private readonly ILogger<SwitchedOtpSmsSender> _logger;

    public SwitchedOtpSmsSender(IOtpSmsSender inner, ISmsSwitch smsSwitch, ILogger<SwitchedOtpSmsSender> logger)
    {
        _inner = inner;
        _switch = smsSwitch;
        _logger = logger;
    }

    public async Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        if (!await _switch.IsOnAsync(cancellationToken))
        {
            _logger.LogInformation("SMS sending is switched off — not sending a code to {Phone}.", toPhone);
            return;
        }

        await _inner.SendAsync(toPhone, message, cancellationToken);
    }
}
