namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `countries` table — BRD §6.1.</summary>
public class Country
{
    public int Id { get; set; }
    public string Sortname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
