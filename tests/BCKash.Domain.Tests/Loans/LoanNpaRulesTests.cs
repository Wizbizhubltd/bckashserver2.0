using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanNpaRulesTests
{
    [Theory]
    [InlineData(0, 90, false)]
    [InlineData(90, 90, false)] // exactly at the threshold — not yet over
    [InlineData(91, 90, true)]
    [InlineData(200, 90, true)]
    public void IsNpa_flags_once_arrears_exceed_the_product_threshold(int daysInArrears, int npaDays, bool expected)
    {
        Assert.Equal(expected, LoanNpaRules.IsNpa(daysInArrears, npaDays));
    }

    [Fact]
    public void IsNpa_is_false_when_the_product_has_no_configured_threshold()
    {
        Assert.False(LoanNpaRules.IsNpa(9999, null));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldSuspendIncome_requires_both_NPA_and_the_product_flag(bool isNpa, bool npaSuspendIncome, bool expected)
    {
        Assert.Equal(expected, LoanNpaRules.ShouldSuspendIncome(isNpa, npaSuspendIncome));
    }
}
