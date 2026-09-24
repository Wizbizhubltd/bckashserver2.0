namespace BCKash.Domain.Organization;

/// <summary>A local government area within a <see cref="State"/> — fixed reference data, seeded at startup.</summary>
public class Lga
{
    public int Id { get; set; }
    public int StateId { get; set; }
    public string Name { get; set; } = string.Empty;

    public State State { get; set; } = null!;
}
