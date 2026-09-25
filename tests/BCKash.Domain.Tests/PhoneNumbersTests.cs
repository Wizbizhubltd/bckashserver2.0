using BCKash.SharedKernel;
using Xunit;

namespace BCKash.Domain.Tests;

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("08031234567", "+2348031234567")]
    [InlineData("0803 123 4567", "+2348031234567")]
    [InlineData("0803-123-4567", "+2348031234567")]
    [InlineData("8031234567", "+2348031234567")]
    [InlineData("2348031234567", "+2348031234567")]
    [InlineData("+2348031234567", "+2348031234567")]
    [InlineData("+234 803 123 4567", "+2348031234567")]
    [InlineData("+23408031234567", "+2348031234567")]
    public void Normalizes_to_plus_234_without_the_leading_zero(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumbers.ToNigerianInternational(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Leaves_blank_values_alone(string? input)
    {
        Assert.Equal(input, PhoneNumbers.ToNigerianInternational(input));
    }
}
