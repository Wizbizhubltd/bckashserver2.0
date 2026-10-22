using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Domain.Tests.Clients;

public class ClientOnboardingRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    [Theory]
    [InlineData("29-Oct-74", 1974, 10, 29)]
    [InlineData("01-Jan-90", 1990, 1, 1)]
    [InlineData("15-Mar-05", 2005, 3, 15)]
    [InlineData("15-Mar-30", 1930, 3, 15)]
    [InlineData("1985-06-12", 1985, 6, 12)]
    [InlineData("12/06/1985", 1985, 6, 12)]
    [InlineData(" 03-Feb-1999 ", 1999, 2, 3)]
    public void ParseBvnBirthDate_reads_provider_formats_and_never_lands_in_the_future(string value, int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), ClientOnboardingRules.ParseBvnBirthDate(value, Today));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseBvnBirthDate_is_null_when_missing_or_unreadable(string? value) =>
        Assert.Null(ClientOnboardingRules.ParseBvnBirthDate(value, Today));
}
