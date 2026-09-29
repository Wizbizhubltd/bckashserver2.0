using BCKash.Application.Organization;
using Xunit;

namespace BCKash.Application.Tests.Organization;

public class CurrencyDisplayTests
{
    [Theory]
    [InlineData("₦", CurrencySymbolPosition.Left, 5000, "₦5,000")]
    [InlineData("₦", CurrencySymbolPosition.Right, 5000, "5,000₦")]
    [InlineData("₦", CurrencySymbolPosition.Left, 1234.5, "₦1,234.50")]
    [InlineData("NGN", CurrencySymbolPosition.Left, 5000, "NGN 5,000")]
    [InlineData("₦", CurrencySymbolPosition.Left, -250, "-₦250")]
    public void Formats_amounts_with_the_configured_symbol_and_position(string symbol, CurrencySymbolPosition position, decimal amount, string expected) =>
        Assert.Equal(expected, new CurrencyDisplay(symbol, position).Format(amount));

    [Theory]
    [InlineData(CurrencyDisplayKeys.Symbol, "")]
    [InlineData(CurrencyDisplayKeys.Symbol, "NAIRA!")]
    [InlineData(CurrencyDisplayKeys.Position, "middle")]
    public void Rejects_invalid_values(string key, string value) =>
        Assert.NotNull(CurrencyDisplayKeys.Validate(key, value));
}
