using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Domain.Tests.Organization;

public class CustomFieldValueValidatorTests
{
    private static CustomField MakeField(CustomFieldType type, bool required = false, string? radio = null, string? checkbox = null, string? select = null) => new()
    {
        Name = "Test Field",
        FieldType = type,
        Required = required,
        RadioBoxValues = radio,
        CheckboxValues = checkbox,
        SelectValues = select,
    };

    [Fact]
    public void Required_field_rejects_empty_value()
    {
        var field = MakeField(CustomFieldType.Textfield, required: true);
        Assert.NotNull(CustomFieldValueValidator.Validate(field, ""));
    }

    [Fact]
    public void Optional_field_accepts_empty_value()
    {
        var field = MakeField(CustomFieldType.Textfield, required: false);
        Assert.Null(CustomFieldValueValidator.Validate(field, null));
    }

    [Theory]
    [InlineData("42", true)]
    [InlineData("not-a-number", false)]
    public void Number_field_requires_a_whole_number(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Number);
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }

    [Theory]
    [InlineData("42.75", true)]
    [InlineData("abc", false)]
    public void Decimal_field_requires_a_decimal_number(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Decimal);
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }

    [Theory]
    [InlineData("2026-01-15", true)]
    [InlineData("not-a-date", false)]
    public void Date_field_requires_a_valid_date(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Date);
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }

    [Fact]
    public void Textarea_field_accepts_any_text()
    {
        var field = MakeField(CustomFieldType.Textarea);
        Assert.Null(CustomFieldValueValidator.Validate(field, "any free text at all"));
    }

    [Theory]
    [InlineData("Red", true)]
    [InlineData("Purple", false)]
    public void Radiobox_field_restricts_to_configured_options(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Radiobox, radio: "Red,Green,Blue");
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }

    [Theory]
    [InlineData("Red", true)]
    [InlineData("Purple", false)]
    public void Select_field_restricts_to_configured_options(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Select, select: "Red,Green,Blue");
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }

    [Theory]
    [InlineData("Red,Blue", true)]
    [InlineData("Red,Purple", false)]
    public void Checkbox_field_allows_multiple_values_from_the_configured_options(string value, bool valid)
    {
        var field = MakeField(CustomFieldType.Checkbox, checkbox: "Red,Green,Blue");
        Assert.Equal(valid, CustomFieldValueValidator.Validate(field, value) is null);
    }
}
