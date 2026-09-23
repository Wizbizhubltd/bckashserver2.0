namespace BCKash.Domain.Organization;

/// <summary>Legacy values of `payment_type_details.type` (note: singular "share", unlike `charges.product`'s plural "shares").</summary>
public enum PaymentTypeDetailReferenceType
{
    Loan,
    Savings,
    Share,
    Client,
    Journal,
}
