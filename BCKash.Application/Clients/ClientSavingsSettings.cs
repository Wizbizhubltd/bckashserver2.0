using System.Globalization;

namespace BCKash.Application.Clients;

/// <summary>
/// Settings → Loan → "Client savings", as fractions (0.025 = 2.5%). <c>Rate</c> is the share of each repayment
/// that goes into the client's savings — fixed onto a loan when it's disbursed, so changing it only affects
/// loans disbursed afterwards; 0 turns savings off for new loans. <c>EarlyWithdrawalFeeRate</c> is what's kept
/// when a client cashes out while a loan is still running.
/// </summary>
public record ClientSavingsSettings(decimal Rate, decimal EarlyWithdrawalFeeRate)
{
    public static readonly ClientSavingsSettings Default = new(0.025m, 0.15m);
}

public static class ClientSavingsSettingKeys
{
    /// <summary>Percent of each repayment saved, e.g. "2.5".</summary>
    public const string Rate = "client_savings_rate";

    /// <summary>Percent kept on an early cash-out, e.g. "15".</summary>
    public const string EarlyWithdrawalFee = "client_savings_early_withdrawal_fee";

    /// <summary>Above this, too little of each repayment would reach the loan.</summary>
    public const decimal MaxRatePercent = 50;

    public static readonly string[] All = [Rate, EarlyWithdrawalFee];

    /// <summary>A stored percentage, or null when it's missing or not a number.</summary>
    public static decimal? ParsePercent(string? value) =>
        decimal.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) ? percent : null;

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value)
    {
        if (!All.Contains(key))
        {
            return null;
        }

        var percent = ParsePercent(value);
        return key switch
        {
            Rate when percent is not (>= 0 and <= MaxRatePercent) =>
                $"The savings share must be a percentage from 0 to {MaxRatePercent:0} — 0 turns savings off for new loans.",
            EarlyWithdrawalFee when percent is not (>= 0 and <= 100) =>
                "The early cash-out charge must be a percentage from 0 to 100.",
            _ => null,
        };
    }
}

public interface IClientSavingsSettingsProvider
{
    /// <summary>Missing or invalid settings fall back to <see cref="ClientSavingsSettings.Default"/> (2.5% saved, 15% early cash-out charge).</summary>
    Task<ClientSavingsSettings> GetAsync(CancellationToken cancellationToken = default);
}
