namespace BCKash.Api.Contracts;

public record SettingResponse(int Id, string SettingKey, string? SettingValue);

public record CreateSettingRequest(string SettingKey, string? SettingValue);

public record UpdateSettingRequest(string? SettingValue);
