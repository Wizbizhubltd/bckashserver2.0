using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Domain.Tests.Clients;

public class ClientAccountNumberFormatTests
{
    [Theory]
    [InlineData(1, "CL00000001")]
    [InlineData(42, "CL00000042")]
    [InlineData(12345678, "CL12345678")]
    public void Format_pads_to_eight_digits_with_the_CL_prefix(long sequence, string expected)
    {
        Assert.Equal(expected, ClientAccountNumberFormat.Format(sequence));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999999)]
    public void TryParseSequence_round_trips_Format(long sequence)
    {
        var formatted = ClientAccountNumberFormat.Format(sequence);

        Assert.True(ClientAccountNumberFormat.TryParseSequence(formatted, out var parsed));
        Assert.Equal(sequence, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("legacy-123")]
    [InlineData("CL123")]
    [InlineData("CL1234567890")]
    [InlineData("XX00000001")]
    public void TryParseSequence_rejects_anything_not_matching_the_format(string? accountNo)
    {
        Assert.False(ClientAccountNumberFormat.TryParseSequence(accountNo, out _));
    }
}
