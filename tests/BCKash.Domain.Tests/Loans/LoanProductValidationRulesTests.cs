using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanProductValidationRulesTests
{
    // decimal/decimal? can't appear in [InlineData] (not a valid attribute-argument type), and
    // xUnit's int->nullable-numeric widening for InlineData doesn't reliably apply to nullable
    // parameter types via reflection — so these two theories use [MemberData], which supplies
    // already-typed decimal? values directly, bypassing that conversion entirely.
    public static IEnumerable<object?[]> MinDefaultMaxCases()
    {
        yield return [100m, 500m, 1000m, true];
        yield return [500m, 100m, 1000m, false]; // min > default
        yield return [100m, 1500m, 1000m, false]; // default > max
        yield return [1000m, 500m, 100m, false]; // min > max
        yield return [null, null, null, true];
        yield return [100m, null, 1000m, true]; // default unconstrained
        yield return [null, 500m, null, true]; // min/max unconstrained
    }

    [Theory]
    [MemberData(nameof(MinDefaultMaxCases))]
    public void IsValidMinDefaultMax_decimal_enforces_min_le_default_le_max(decimal? min, decimal? @default, decimal? max, bool expected)
    {
        Assert.Equal(expected, LoanProductValidationRules.IsValidMinDefaultMax(min, @default, max));
    }

    [Theory]
    [InlineData(6, 12, 24, true)]
    [InlineData(12, 6, 24, false)]
    [InlineData(6, 30, 24, false)]
    [InlineData(24, 12, 6, false)]
    public void IsValidMinDefaultMax_int_enforces_min_le_default_le_max(int? min, int? @default, int? max, bool expected)
    {
        Assert.Equal(expected, LoanProductValidationRules.IsValidMinDefaultMax(min, @default, max));
    }

    public static IEnumerable<object?[]> WithinRangeCases()
    {
        yield return [500m, 100m, 1000m, true];
        yield return [50m, 100m, 1000m, false];
        yield return [1500m, 100m, 1000m, false];
        yield return [500m, null, null, true];
    }

    [Theory]
    [MemberData(nameof(WithinRangeCases))]
    public void IsWithinRange_decimal(decimal value, decimal? min, decimal? max, bool expected)
    {
        Assert.Equal(expected, LoanProductValidationRules.IsWithinRange(value, min, max));
    }

    [Theory]
    [InlineData(12, 6, 24, true)]
    [InlineData(3, 6, 24, false)]
    [InlineData(30, 6, 24, false)]
    public void IsWithinRange_int(int value, int? min, int? max, bool expected)
    {
        Assert.Equal(expected, LoanProductValidationRules.IsWithinRange(value, min, max));
    }
}
