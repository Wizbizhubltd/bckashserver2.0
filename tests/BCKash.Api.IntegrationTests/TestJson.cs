using System.Text.Json;
using System.Text.Json.Serialization;

namespace BCKash.Api.IntegrationTests;

/// <summary>
/// The server serializes enums as strings (see Program.cs's JsonStringEnumConverter),
/// but System.Text.Json.HttpContentJsonExtensions.ReadFromJsonAsync uses plain default
/// options unless given these explicitly — needed wherever a response DTO has an enum
/// property (e.g. CustomFieldResponse.FieldType, ChargeResponse.Product).
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
