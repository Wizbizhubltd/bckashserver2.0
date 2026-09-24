namespace BCKash.Domain.Organization;

/// <summary>A Nigerian state (or the FCT) — fixed reference data, seeded at startup.</summary>
public class State
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Lga> Lgas { get; set; } = new List<Lga>();
}
