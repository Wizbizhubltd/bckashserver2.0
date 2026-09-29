using BCKash.Application.Clients;

namespace BCKash.Api.IntegrationTests;

/// <summary>
/// The test host's stand-in for the BVN gateway — predictable, so every onboarding path can be
/// exercised: a BVN starting with 0 isn't found; one ending in 9 comes back registered to a
/// different surname and phone (a mismatch); any other matches what was typed.
/// </summary>
public class FakeBvnVerificationProvider : IBvnVerificationProvider
{
    public Task<BvnLookupResult> LookupAsync(string bvn, string firstName, string? middleName, string lastName, string? phone, CancellationToken cancellationToken = default)
    {
        if (bvn.StartsWith('0'))
        {
            return Task.FromResult(new BvnLookupResult(BvnLookupOutcome.NotFound));
        }

        var mismatch = bvn.EndsWith('9');
        return Task.FromResult(new BvnLookupResult(
            BvnLookupOutcome.Found,
            FirstName: firstName.ToUpperInvariant(),
            LastName: mismatch ? "ADEBAYO" : lastName.ToUpperInvariant(),
            Phone: mismatch ? "08000000000" : null,
            BirthDate: "01-Jan-90"));
    }
}
