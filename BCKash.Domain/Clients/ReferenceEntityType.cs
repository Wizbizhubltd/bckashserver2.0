namespace BCKash.Domain.Clients;

/// <summary>
/// Matches the shared `type` enum used by both the legacy `documents` and `notes` tables —
/// the polymorphic kind of record a document/note is attached to. Values are serialized as
/// these strings.
/// </summary>
public enum ReferenceEntityType
{
    Client,
    Loan,
    Group,
    Savings,
    Identification,
    Shares,
    Repayment,
}
