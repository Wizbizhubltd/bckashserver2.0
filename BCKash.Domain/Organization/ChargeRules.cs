namespace BCKash.Domain.Organization;

/// <summary>
/// Business and credit-risk rules for defining a fee or penalty, on top of
/// <see cref="ChargeValidationRules"/>' product scoping:
/// <list type="bullet">
/// <item>Whether a charge is a penalty follows from its type, never from a separate flag — a late
/// fee recorded as an ordinary fee would be reported, allocated and waived as the wrong thing.</item>
/// <item>A percentage must say what it's a percentage of, and only bases proportionate to the event
/// are allowed: a late fee on the missed instalment (not the whole loan), a default penalty on what's
/// still owed, an early-closure fee on the outstanding balance.</item>
/// <item>Penalty controls (grace, repetition, a total cap) apply only to the penalties they make sense
/// for; a repeating penalty must carry a cap so it can't compound without limit.</item>
/// </list>
/// </summary>
public static class ChargeRules
{
    public const int MaxGraceDays = 365;
    public const int MaxRepeatEveryDays = 365;

    public static readonly IReadOnlySet<ChargeType> PenaltyTypes = new HashSet<ChargeType>
    {
        ChargeType.OverdueInstallmentFee,
        ChargeType.OverdueMaturity,
        ChargeType.EarlyRepayment,
    };

    /// <summary>Penalties triggered by lateness — the ones grace days, repetition and caps apply to.</summary>
    private static readonly HashSet<ChargeType> LatenessPenalties = [ChargeType.OverdueInstallmentFee, ChargeType.OverdueMaturity];

    private static readonly ChargeOption[] InstallmentBases =
    [
        ChargeOption.InstallmentPrincipalDue,
        ChargeOption.InstallmentPrincipalInterestDue,
        ChargeOption.InstallmentInterestDue,
        ChargeOption.InstallmentTotalDue,
    ];

    /// <summary>How the amount may be calculated for each loan charge type — Flat always, plus the percentage bases that fit.</summary>
    private static readonly Dictionary<ChargeType, ChargeOption[]> LoanOptions = new()
    {
        [ChargeType.Disbursement] = [ChargeOption.Flat, ChargeOption.OriginalPrincipal],
        [ChargeType.DisbursementRepayment] = [ChargeOption.Flat, ChargeOption.OriginalPrincipal],
        [ChargeType.SpecifiedDueDate] = [ChargeOption.Flat, ChargeOption.OriginalPrincipal, ChargeOption.TotalOutstanding],
        [ChargeType.InstallmentFee] = [ChargeOption.Flat, ChargeOption.OriginalPrincipal, .. InstallmentBases],
        [ChargeType.LoanReschedulingFee] = [ChargeOption.Flat, ChargeOption.OriginalPrincipal, ChargeOption.PrincipalDue, ChargeOption.TotalOutstanding],
        [ChargeType.OverdueInstallmentFee] = [ChargeOption.Flat, .. InstallmentBases],
        [ChargeType.OverdueMaturity] = [ChargeOption.Flat, ChargeOption.PrincipalDue, ChargeOption.TotalDue, ChargeOption.TotalOutstanding],
        [ChargeType.EarlyRepayment] = [ChargeOption.Flat, ChargeOption.PrincipalDue, ChargeOption.TotalOutstanding],
        [ChargeType.ApplicationFormFee] = [ChargeOption.Flat],
    };

    public static bool IsPenalty(ChargeType type) => PenaltyTypes.Contains(type);

    public static bool IsPercentage(ChargeOption option) => option != ChargeOption.Flat;

    public static IReadOnlyList<ChargeOption> AllowedOptions(ChargeType type, ChargeProduct product) => product switch
    {
        ChargeProduct.Loan => LoanOptions.GetValueOrDefault(type, [ChargeOption.Flat]),
        // A client's or group's dues have no transaction amount to take a percentage of.
        ChargeProduct.Client or ChargeProduct.Group => [ChargeOption.Flat],
        _ => [ChargeOption.Flat, ChargeOption.Percentage],
    };

    public record Definition(
        string? Name, ChargeProduct Product, ChargeType Type, ChargeOption Option,
        decimal? Amount, decimal? MinimumAmount, decimal? MaximumAmount,
        int? GraceDays, int? RepeatEveryDays, decimal? MaxTotalPercent, int? FreeAfterInstallments);

    /// <summary>Null when <paramref name="d"/> is a valid fee or penalty definition; otherwise what's wrong, in plain words.</summary>
    public static string? Validate(Definition d)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) return "Give the fee a name.";
        if (!ChargeValidationRules.IsValidChargeTypeForProduct(d.Type, d.Product)) return $"A {Describe(d.Type)} can't be applied at {d.Product.ToString().ToLowerInvariant()} level.";
        if (!AllowedOptions(d.Type, d.Product).Contains(d.Option)) return $"That way of calculating the amount isn't allowed for a {Describe(d.Type)}.";

        if (d.Amount is not > 0) return "The amount must be more than zero.";
        if (IsPercentage(d.Option) && d.Amount > 100) return "A percentage can't be more than 100.";
        if (d.MinimumAmount is < 0 || d.MaximumAmount is < 0) return "Minimum and maximum amounts can't be negative.";
        if (d.MinimumAmount is { } min && d.MaximumAmount is { } max && min > max) return "The minimum amount can't be more than the maximum.";
        if (!IsPercentage(d.Option) && (d.MinimumAmount.HasValue || d.MaximumAmount.HasValue)) return "Minimum and maximum amounts only apply to percentage-based fees.";

        var lateness = LatenessPenalties.Contains(d.Type);
        if (!lateness && (d.GraceDays.HasValue || d.RepeatEveryDays.HasValue || d.MaxTotalPercent.HasValue))
            return "Grace days, repeating and a total cap only apply to late-repayment and default penalties.";
        if (d.GraceDays is < 0 or > MaxGraceDays) return $"Grace days must be between 0 and {MaxGraceDays}.";
        if (d.RepeatEveryDays is < 1 or > MaxRepeatEveryDays) return $"A repeating penalty must repeat every 1 to {MaxRepeatEveryDays} days.";
        if (d.MaxTotalPercent is <= 0 or > 100) return "The total penalty cap must be more than 0% and at most 100% of the amount disbursed.";
        if (d.RepeatEveryDays.HasValue && !d.MaxTotalPercent.HasValue)
            return "A penalty that repeats needs a total cap, so it can't keep growing for as long as the loan is unpaid.";

        if (d.Type != ChargeType.EarlyRepayment && d.FreeAfterInstallments.HasValue) return "“No fee after N instalments” only applies to early-closure fees.";
        if (d.FreeAfterInstallments is < 1) return "“No fee after N instalments” must be at least 1.";

        return null;
    }

    private static string Describe(ChargeType type) => type switch
    {
        ChargeType.OverdueInstallmentFee => "late repayment fee",
        ChargeType.OverdueMaturity => "loan default penalty",
        ChargeType.EarlyRepayment => "early closure fee",
        _ => "fee of this type",
    };
}
