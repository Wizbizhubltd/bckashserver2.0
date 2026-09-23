using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanAccountNumberFormatTests
{
    [Theory]
    [InlineData(1, "LN00000001")]
    [InlineData(42, "LN00000042")]
    [InlineData(12345678, "LN12345678")]
    public void Format_pads_to_eight_digits_with_the_LN_prefix(long sequence, string expected)
    {
        Assert.Equal(expected, LoanAccountNumberFormat.Format(sequence));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999999)]
    public void TryParseSequence_round_trips_Format(long sequence)
    {
        var formatted = LoanAccountNumberFormat.Format(sequence);

        Assert.True(LoanAccountNumberFormat.TryParseSequence(formatted, out var parsed));
        Assert.Equal(sequence, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CL00000001")]
    [InlineData("LN123")]
    [InlineData("LN1234567890")]
    public void TryParseSequence_rejects_anything_not_matching_the_format(string? accountNumber)
    {
        Assert.False(LoanAccountNumberFormat.TryParseSequence(accountNumber, out _));
    }
}
