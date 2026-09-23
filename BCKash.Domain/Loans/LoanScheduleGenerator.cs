namespace BCKash.Domain.Loans;

/// <summary>
/// Every field the schedule generator needs off a Loan + its LoanProduct. <see cref="InterestRate"/>
/// is the nominal rate as a plain percentage number (e.g. 12 means 12%), in <see cref="InterestRateType"/>
/// units.
/// </summary>
public record ScheduleGenerationInput(
    decimal Principal,
    decimal InterestRate,
    InterestRateFrequencyType InterestRateType,
    int LoanTerm,
    FrequencyType LoanTermType,
    int RepaymentFrequency,
    FrequencyType RepaymentFrequencyType,
    LoanInterestMethod InterestMethod,
    LoanAmortizationMethod AmortizationMethod,
    InterestCalculationPeriodType CalculationPeriodType,
    YearDaysType YearDays,
    MonthDaysType MonthDays,
    int GraceOnPrincipal,
    int GraceOnInterestCharged,
    int GraceOnInterestPayment,
    DateOnly DisbursementDate);

public record ScheduleInstallment(int Number, DateOnly DueDate, decimal Principal, decimal Interest);

/// <summary>
/// FR-LN-12/13/14's repayment schedule generator — see docs/interest-calculation-spec.md for the
/// full formula writeup and worked examples. <b>This is a best-effort implementation of standard
/// textbook microfinance amortization math, NOT extracted from or validated against BCKash's
/// legacy PHP codebase or real historical loans</b> — FR-LN-15 remains formally unresolved; see
/// the spec doc's header for the tracked follow-up. Pure function, no I/O, so every documented
/// worked example is directly unit-testable.
/// </summary>
public static class LoanScheduleGenerator
{
    public static IReadOnlyList<ScheduleInstallment> Generate(ScheduleGenerationInput input)
    {
        var repaymentPeriodDays = PeriodDays(input.RepaymentFrequencyType, input.RepaymentFrequency, input.YearDays, input.MonthDays);
        var termDays = PeriodDays(input.LoanTermType, input.LoanTerm, input.YearDays, input.MonthDays);
        var n = Math.Max(1, (int)Math.Round(termDays / repaymentPeriodDays, MidpointRounding.AwayFromZero));

        var graceOnPrincipal = Math.Clamp(input.GraceOnPrincipal, 0, n - 1);
        var graceOnInterestCharged = Math.Clamp(input.GraceOnInterestCharged, 0, n);
        var graceOnInterestPayment = Math.Clamp(input.GraceOnInterestPayment, 0, n - 1);

        var dailyRate = (input.InterestRate / 100m) / RateUnitDays(input.InterestRateType, input.YearDays, input.MonthDays);
        var periodRate = dailyRate * repaymentPeriodDays;

        var dueDates = new DateOnly[n + 1]; // 1-based
        var current = input.DisbursementDate;
        for (var p = 1; p <= n; p++)
        {
            current = AddPeriod(current, input.RepaymentFrequencyType, input.RepaymentFrequency);
            dueDates[p] = current;
        }

        var (principals, interests) = input.InterestMethod == LoanInterestMethod.Flat
            ? GenerateFlat(input.Principal, periodRate, n, graceOnPrincipal, graceOnInterestCharged, graceOnInterestPayment)
            : GenerateDecliningBalance(input.Principal, periodRate, n, input.AmortizationMethod, graceOnPrincipal, graceOnInterestCharged, graceOnInterestPayment);

        TrueUpPrincipal(principals, input.Principal);

        var result = new List<ScheduleInstallment>(n);
        for (var p = 1; p <= n; p++)
        {
            result.Add(new ScheduleInstallment(p, dueDates[p], principals[p], interests[p]));
        }

        return result;
    }

    private static (decimal[] Principals, decimal[] Interests) GenerateFlat(
        decimal principal, decimal periodRate, int n, int graceOnPrincipal, int graceOnInterestCharged, int graceOnInterestPayment)
    {
        var principalBearingCount = n - graceOnPrincipal;
        var interestChargedCount = n - graceOnInterestCharged;

        var totalInterest = Math.Round(principal * periodRate * interestChargedCount, 2, MidpointRounding.AwayFromZero);
        var perInterest = interestChargedCount > 0 ? Math.Round(totalInterest / interestChargedCount, 2, MidpointRounding.AwayFromZero) : 0m;
        var perPrincipal = principalBearingCount > 0 ? Math.Round(principal / principalBearingCount, 2, MidpointRounding.AwayFromZero) : 0m;

        var principals = new decimal[n + 1];
        var interests = new decimal[n + 1];

        for (var p = 1; p <= n; p++)
        {
            principals[p] = p <= graceOnPrincipal ? 0m : perPrincipal;
            interests[p] = p <= graceOnInterestCharged ? 0m : perInterest;
        }

        DeferInterestPayment(interests, n, graceOnInterestPayment);

        return (principals, interests);
    }

    private static (decimal[] Principals, decimal[] Interests) GenerateDecliningBalance(
        decimal principal, decimal periodRate, int n, LoanAmortizationMethod amortizationMethod,
        int graceOnPrincipal, int graceOnInterestCharged, int graceOnInterestPayment)
    {
        var principals = new decimal[n + 1];
        var interests = new decimal[n + 1];
        var balance = principal;

        if (amortizationMethod == LoanAmortizationMethod.EqualPrincipal)
        {
            var principalBearingCount = n - graceOnPrincipal;
            var perPrincipal = principalBearingCount > 0 ? Math.Round(principal / principalBearingCount, 2, MidpointRounding.AwayFromZero) : 0m;

            for (var p = 1; p <= n; p++)
            {
                var accruedInterest = p <= graceOnInterestCharged ? 0m : Math.Round(balance * periodRate, 2, MidpointRounding.AwayFromZero);
                var principalThisPeriod = p <= graceOnPrincipal ? 0m : perPrincipal;

                interests[p] = accruedInterest;
                principals[p] = principalThisPeriod;
                balance -= principalThisPeriod;
            }
        }
        else
        {
            var amortizedPeriods = n - graceOnPrincipal;
            var installment = LevelInstallment(principal, periodRate, amortizedPeriods);

            for (var p = 1; p <= n; p++)
            {
                var accruedInterest = p <= graceOnInterestCharged ? 0m : Math.Round(balance * periodRate, 2, MidpointRounding.AwayFromZero);
                interests[p] = accruedInterest;

                if (p <= graceOnPrincipal)
                {
                    principals[p] = 0m;
                    continue;
                }

                // Cash-flow principal is derived from the level installment against the *accrued*
                // interest (which may be zero during a GraceOnInterestCharged window), not the
                // other way round — so an interest-charged grace period still reduces the balance
                // via the full installment amount.
                var principalThisPeriod = Math.Min(balance, Math.Round(installment - accruedInterest, 2, MidpointRounding.AwayFromZero));
                principals[p] = Math.Max(0m, principalThisPeriod);
                balance -= principals[p];
            }
        }

        DeferInterestPayment(interests, n, graceOnInterestPayment);

        return (principals, interests);
    }

    /// <summary>Standard annuity formula: installment = P*r*(1+r)^n / ((1+r)^n - 1); falls back to straight-line if r is 0 (interest-free loan).</summary>
    private static decimal LevelInstallment(decimal principal, decimal periodRate, int periods)
    {
        if (periods <= 0)
        {
            return 0m;
        }

        if (periodRate == 0m)
        {
            return principal / periods;
        }

        var onePlusR = 1m + periodRate;
        var factor = Pow(onePlusR, periods);
        return principal * periodRate * factor / (factor - 1m);
    }

    private static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }

    /// <summary>Interest accrued in the first <paramref name="graceOnInterestPayment"/> periods is still owed, just not due yet — it's deferred as a lump sum onto the first period after the payment grace window.</summary>
    private static void DeferInterestPayment(decimal[] interests, int n, int graceOnInterestPayment)
    {
        if (graceOnInterestPayment <= 0)
        {
            return;
        }

        var carry = 0m;
        for (var p = 1; p <= graceOnInterestPayment; p++)
        {
            carry += interests[p];
            interests[p] = 0m;
        }

        if (graceOnInterestPayment + 1 <= n)
        {
            interests[graceOnInterestPayment + 1] += carry;
        }
    }

    /// <summary>Rounding across periods can leave the schedule's total principal a cent or two off the disbursed amount — true it up on the last installment so the loan balance reaches exactly zero.</summary>
    private static void TrueUpPrincipal(decimal[] principals, decimal principal)
    {
        var sum = 0m;
        for (var p = 1; p < principals.Length; p++)
        {
            sum += principals[p];
        }

        var diff = principal - sum;
        if (diff != 0m && principals.Length > 1)
        {
            principals[^1] += diff;
        }
    }

    private static int YearDaysValue(YearDaysType type) => type switch
    {
        YearDaysType.Days360 => 360,
        YearDaysType.Days364 => 364,
        YearDaysType.Days365 => 365,
        YearDaysType.Actual => 365,
        _ => 365,
    };

    private static int MonthDaysValue(MonthDaysType type) => type switch
    {
        MonthDaysType.Days30 => 30,
        MonthDaysType.Days31 => 31,
        MonthDaysType.Actual => 30,
        _ => 30,
    };

    private static decimal PeriodDays(FrequencyType type, int multiplier, YearDaysType yearDays, MonthDaysType monthDays) => type switch
    {
        FrequencyType.Days => multiplier,
        FrequencyType.Weeks => multiplier * 7,
        FrequencyType.Months => multiplier * MonthDaysValue(monthDays),
        FrequencyType.Years => multiplier * YearDaysValue(yearDays),
        _ => multiplier,
    };

    private static decimal RateUnitDays(InterestRateFrequencyType type, YearDaysType yearDays, MonthDaysType monthDays) => type switch
    {
        InterestRateFrequencyType.Day => 1,
        InterestRateFrequencyType.Week => 7,
        InterestRateFrequencyType.Month => MonthDaysValue(monthDays),
        InterestRateFrequencyType.Year => YearDaysValue(yearDays),
        _ => 1,
    };

    private static DateOnly AddPeriod(DateOnly date, FrequencyType type, int multiplier) => type switch
    {
        FrequencyType.Days => date.AddDays(multiplier),
        FrequencyType.Weeks => date.AddDays(multiplier * 7),
        FrequencyType.Months => date.AddMonths(multiplier),
        FrequencyType.Years => date.AddYears(multiplier),
        _ => date.AddDays(multiplier),
    };
}
