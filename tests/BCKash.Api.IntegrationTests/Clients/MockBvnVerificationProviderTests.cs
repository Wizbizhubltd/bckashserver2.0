using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>The UseMockBvn setting: isAMatch, isConflicting, or unset for the real gateway.</summary>
public class MockBvnVerificationProviderTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("isAMatch", MockBvnMode.Match)]
    [InlineData("ISAMATCH", MockBvnMode.Match)]
    [InlineData("isConflicting", MockBvnMode.Conflicting)]
    public void Parses_the_setting(string? value, MockBvnMode? expected) =>
        Assert.Equal(expected, MockBvnVerificationProvider.ParseMode(value));

    [Fact]
    public void An_unknown_value_stops_startup_instead_of_quietly_using_the_real_gateway() =>
        Assert.Throws<InvalidOperationException>(() => MockBvnVerificationProvider.ParseMode("isMatch"));

    [Fact]
    public async Task IsAMatch_returns_exactly_what_was_submitted()
    {
        var result = await new MockBvnVerificationProvider(MockBvnMode.Match).LookupAsync("22222222222", "Ada", "Chioma", "Obi", "08031234567");

        Assert.Equal(BvnLookupOutcome.Found, result.Outcome);
        Assert.True(ClientOnboardingRules.NamesMatch("Ada", result.FirstName));
        Assert.True(ClientOnboardingRules.NamesMatch("Chioma", result.MiddleName));
        Assert.True(ClientOnboardingRules.NamesMatch("Obi", result.LastName));
        Assert.True(ClientOnboardingRules.PhonesMatch("08031234567", result.Phone));
    }

    [Fact]
    public async Task IsConflicting_returns_details_that_differ_from_what_was_submitted()
    {
        var provider = new MockBvnVerificationProvider(MockBvnMode.Conflicting);
        for (var i = 0; i < 50; i++)
        {
            // Submitting names from the mock's own lists proves it never hands the same one back.
            var result = await provider.LookupAsync("22222222222", "CHIDINMA", "ADAEZE", "OKAFOR", "08031234567");

            Assert.Equal(BvnLookupOutcome.Found, result.Outcome);
            Assert.False(ClientOnboardingRules.NamesMatch("CHIDINMA", result.FirstName));
            Assert.False(ClientOnboardingRules.NamesMatch("ADAEZE", result.MiddleName));
            Assert.False(ClientOnboardingRules.NamesMatch("OKAFOR", result.LastName));
            Assert.False(ClientOnboardingRules.PhonesMatch("08031234567", result.Phone));
        }
    }
}
