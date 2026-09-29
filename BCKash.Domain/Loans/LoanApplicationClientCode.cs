namespace BCKash.Domain.Loans;

/// <summary>
/// A one-time code sent to a client when staff raise a loan application on their behalf; the
/// staff member must enter it to submit. It's bound to exactly what the client was told — client,
/// product, amount and the staff member raising it — so it can't be reused for a different loan.
/// New (not in the legacy schema).
/// </summary>
public class LoanApplicationClientCode
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int LoanProductId { get; set; }
    public decimal Amount { get; set; }
    public int? RequestedById { get; set; }
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Masked destinations, e.g. "+234•••••6789, j•••@gmail.com" — kept for the audit trail.</summary>
    public string? SentTo { get; set; }

    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>The application it confirmed, once used.</summary>
    public int? LoanApplicationId { get; set; }
}
