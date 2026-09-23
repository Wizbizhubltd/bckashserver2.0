using System.Globalization;

namespace BCKash.Domain.Organization;

/// <summary>
/// Validates a captured <see cref="CustomFieldValue.Value"/> against its
/// <see cref="CustomField.FieldType"/> (FR-ORG-6 / Phase 1 acceptance criteria — every
/// field type must be definable and capturable). Configured option lists
/// (radio/checkbox/select) are stored comma-separated on the field, matching the legacy
/// `radio_box_values`/`checkbox_values`/`select_values` text columns.
/// </summary>
public static class CustomFieldValueValidator
{
    public static string? Validate(CustomField field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return field.Required ? "A value is required for this field." : null;
        }

        return field.FieldType switch
        {
            CustomFieldType.Number => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? null
                : "Value must be a whole number.",
            CustomFieldType.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                ? null
                : "Value must be a decimal number.",
            CustomFieldType.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? null
                : "Value must be a valid date.",
            CustomFieldType.Radiobox => ValidateAgainstOptions(value, field.RadioBoxValues, allowMultiple: false),
            CustomFieldType.Select => ValidateAgainstOptions(value, field.SelectValues, allowMultiple: false),
            CustomFieldType.Checkbox => ValidateAgainstOptions(value, field.CheckboxValues, allowMultiple: true),
            _ => null, // Textfield/Textarea — any non-empty text is acceptable.
        };
    }

    private static string? ValidateAgainstOptions(string value, string? configuredOptions, bool allowMultiple)
    {
        var options = (configuredOptions ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = allowMultiple
            ? value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [value.Trim()];

        return selected.All(options.Contains) ? null : "Value must be one of the field's configured options.";
    }
}
