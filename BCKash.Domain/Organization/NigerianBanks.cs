namespace BCKash.Domain.Organization;

public record Bank(string Name, string Category);

/// <summary>
/// Banks an office bank account can be held with, grouped by CBN licence type. Office bank
/// accounts must use one of these names, so the same bank is never spelled two ways.
/// Update this list as the CBN licenses, merges or revokes banks (e.g. Heritage Bank, revoked 2024,
/// is deliberately absent).
/// </summary>
public static class NigerianBanks
{
    public const string Commercial = "Commercial banks";
    public const string NonInterest = "Non-interest banks";
    public const string Merchant = "Merchant banks";
    public const string Microfinance = "Microfinance banks";
    public const string PaymentService = "Payment service banks";

    public static readonly IReadOnlyList<Bank> All =
    [
        new("Access Bank", Commercial),
        new("Citibank Nigeria", Commercial),
        new("Ecobank Nigeria", Commercial),
        new("Fidelity Bank", Commercial),
        new("First Bank of Nigeria", Commercial),
        new("First City Monument Bank (FCMB)", Commercial),
        new("Globus Bank", Commercial),
        new("Guaranty Trust Bank (GTBank)", Commercial),
        new("Keystone Bank", Commercial),
        new("Optimus Bank", Commercial),
        new("Parallex Bank", Commercial),
        new("Polaris Bank", Commercial),
        new("PremiumTrust Bank", Commercial),
        new("Providus Bank", Commercial),
        new("Signature Bank", Commercial),
        new("Stanbic IBTC Bank", Commercial),
        new("Standard Chartered Bank", Commercial),
        new("Sterling Bank", Commercial),
        new("SunTrust Bank", Commercial),
        new("Titan Trust Bank", Commercial),
        new("Union Bank of Nigeria", Commercial),
        new("United Bank for Africa (UBA)", Commercial),
        new("Unity Bank", Commercial),
        new("Wema Bank", Commercial),
        new("Zenith Bank", Commercial),

        new("Jaiz Bank", NonInterest),
        new("Lotus Bank", NonInterest),
        new("TAJBank", NonInterest),
        new("The Alternative Bank", NonInterest),

        new("Coronation Merchant Bank", Merchant),
        new("FBNQuest Merchant Bank", Merchant),
        new("FSDH Merchant Bank", Merchant),
        new("Greenwich Merchant Bank", Merchant),
        new("Nova Bank", Merchant),
        new("Rand Merchant Bank", Merchant),

        new("AB Microfinance Bank", Microfinance),
        new("Accion Microfinance Bank", Microfinance),
        new("Baobab Microfinance Bank", Microfinance),
        new("Carbon Microfinance Bank", Microfinance),
        new("FairMoney Microfinance Bank", Microfinance),
        new("Kuda Microfinance Bank", Microfinance),
        new("LAPO Microfinance Bank", Microfinance),
        new("Moniepoint Microfinance Bank", Microfinance),
        new("NPF Microfinance Bank", Microfinance),
        new("OPay (Paycom Microfinance Bank)", Microfinance),
        new("PalmPay", Microfinance),
        new("Rubies Microfinance Bank", Microfinance),
        new("Sparkle Microfinance Bank", Microfinance),
        new("VFD Microfinance Bank", Microfinance),

        new("9 Payment Service Bank (9PSB)", PaymentService),
        new("HopePSB", PaymentService),
        new("MoMo Payment Service Bank", PaymentService),
        new("SmartCash Payment Service Bank", PaymentService),
    ];

    /// <summary>The list's spelling of <paramref name="name"/>, or null if it isn't a listed bank.</summary>
    public static string? Canonical(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : All.FirstOrDefault(b => string.Equals(b.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;
}
