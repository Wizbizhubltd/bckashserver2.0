using BCKash.Application.Rbac;
using Xunit;

namespace BCKash.Application.Tests.Rbac;

public class LegacyPermissionsParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_parses_to_no_slugs(string? raw)
    {
        var result = LegacyPermissionsParser.Parse(raw);

        Assert.True(result.WasParsed);
        Assert.Empty(result.Slugs);
    }

    [Fact]
    public void Json_array_is_parsed()
    {
        var result = LegacyPermissionsParser.Parse("[\"users.create\",\"loans.view\"]");

        Assert.True(result.WasParsed);
        Assert.Equal(["users.create", "loans.view"], result.Slugs);
    }

    [Fact]
    public void Comma_separated_list_is_parsed()
    {
        var result = LegacyPermissionsParser.Parse("users.create, loans.view ,clients.edit");

        Assert.True(result.WasParsed);
        Assert.Equal(["users.create", "loans.view", "clients.edit"], result.Slugs);
    }

    [Fact]
    public void Single_bare_slug_is_parsed()
    {
        var result = LegacyPermissionsParser.Parse("users.create");

        Assert.True(result.WasParsed);
        Assert.Equal(["users.create"], result.Slugs);
    }

    [Fact]
    public void Malformed_json_like_input_is_reported_as_unparsed()
    {
        var result = LegacyPermissionsParser.Parse("{not valid json[");

        Assert.False(result.WasParsed);
        Assert.Empty(result.Slugs);
    }

    [Fact]
    public void Duplicates_are_removed_case_insensitively()
    {
        var result = LegacyPermissionsParser.Parse("users.create,Users.Create,users.create");

        Assert.Single(result.Slugs);
    }
}
