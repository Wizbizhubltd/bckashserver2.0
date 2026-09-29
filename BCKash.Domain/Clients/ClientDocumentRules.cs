namespace BCKash.Domain.Clients;

/// <summary>
/// The documents a client is documented with — a NIN slip, a utility bill and one form of ID — each
/// with the number it carries, as a picture or PDF of at most 2 MB. A client has one of each; a new
/// upload replaces the old.
/// </summary>
public static class ClientDocumentRules
{
    public const string NinSlip = "nin_slip";
    public const string UtilityBill = "utility_bill";
    public const string IdCard = "id_card";

    public static readonly IReadOnlyList<string> Categories = [NinSlip, UtilityBill, IdCard];

    public const string DriversLicense = "drivers_license";
    public const string InternationalPassport = "international_passport";
    public const string VotersCard = "voters_card";

    public static readonly IReadOnlyList<string> IdTypes = [DriversLicense, InternationalPassport, VotersCard];

    public const long MaxFileBytes = 2 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

    public static string Label(string category, string? idType = null) => category switch
    {
        NinSlip => "NIN slip",
        UtilityBill => "Utility bill",
        IdCard => idType switch
        {
            DriversLicense => "Driver's licence",
            InternationalPassport => "International passport",
            VotersCard => "Voter's card",
            _ => "ID card",
        },
        _ => "Document",
    };

    /// <summary>What's wrong with the number entered for a document, or null when it's acceptable.</summary>
    public static string? NumberProblem(string category, string? idType, string? number)
    {
        var value = number?.Trim() ?? string.Empty;
        switch (category)
        {
            case NinSlip:
                return value.Length == 11 && value.All(char.IsDigit) ? null : "A NIN is 11 digits.";
            case UtilityBill:
                return value.Length is >= 3 and <= 30 ? null : "Enter the account or meter number on the bill (3–30 characters).";
            case IdCard:
                if (idType is null || !IdTypes.Contains(idType))
                {
                    return "Choose the type of ID: driver's licence, international passport or voter's card.";
                }

                return value.Length is >= 6 and <= 20 && value.All(char.IsAsciiLetterOrDigit)
                    ? null
                    : "Enter the ID number as printed — 6 to 20 letters and digits, no spaces.";
            default:
                return "Unknown document type.";
        }
    }
}
