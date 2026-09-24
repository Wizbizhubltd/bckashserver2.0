using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Domain.Tests.Organization;

public class OfficeCodeFormatTests
{
    [Fact]
    public void Code_is_BCK_plus_eight_digits_grouped_3_3_2()
    {
        var digits = new Queue<int>([4, 5, 3, 3, 2, 4, 6, 1]);

        var code = OfficeCodeFormat.Generate(digits.Dequeue);

        Assert.Equal("BCK453-324-61", code);
    }

    [Fact]
    public void Code_is_eleven_characters_without_the_dashes()
    {
        var code = OfficeCodeFormat.Generate(() => 7);

        Assert.Equal(11, code.Replace("-", "").Length);
    }
}
