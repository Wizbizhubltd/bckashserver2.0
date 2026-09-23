using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>
/// Maps the legacy `settings` key/value table. Doubles as Phase 0's "dummy audited
/// entity" — its CRUD controller exists purely to prove the audit interceptor works
/// end-to-end (see Phase 0 acceptance criteria); real settings-driven behavior lands
/// as each module needs it.
/// </summary>
public class Setting : IAuditable
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
}
