using BCKash.Application.Organization;
using Xunit;

namespace BCKash.Application.Tests.Organization;

public class CompanyProfileRulesTests
{
    [Theory]
    [InlineData(CompanyProfileKeys.Name, "")]
    [InlineData(CompanyProfileKeys.Name, "   ")]
    [InlineData(CompanyProfileKeys.Email, "info@")]
    [InlineData(CompanyProfileKeys.Email, "info@bckash")]
    [InlineData(CompanyProfileKeys.Website, "bckash.com")]
    [InlineData(CompanyProfileKeys.Website, "ftp://bckash.com")]
    [InlineData(CompanyProfileKeys.PortalAddress, "http://www.")]
    public void Rejects_invalid_values(string key, string value) =>
        Assert.NotNull(CompanyProfileRules.Validate(key, value));

    [Theory]
    [InlineData(CompanyProfileKeys.Name, "BC Kash HighWay")]
    [InlineData(CompanyProfileKeys.Email, "info@bckash.com.ng")]
    [InlineData(CompanyProfileKeys.Email, "")]
    [InlineData(CompanyProfileKeys.Website, "https://bckbackoffice.com/")]
    [InlineData(CompanyProfileKeys.PortalAddress, "")]
    [InlineData("some_other_key", "")]
    public void Accepts_valid_or_optional_values(string key, string value) =>
        Assert.Null(CompanyProfileRules.Validate(key, value));

    [Fact]
    public void Email_footer_lists_only_the_details_that_are_set()
    {
        var footer = new CompanyProfile("Acme Microfinance", "hello@acme.ng", null, null, "1 Marina, Lagos").EmailFooter;

        Assert.Contains("Acme Microfinance\n1 Marina, Lagos\nhello@acme.ng", footer);
        Assert.DoesNotContain("·", footer);
    }
}
