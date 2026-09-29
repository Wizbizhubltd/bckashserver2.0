using System.Net.Mail;
using System.Text;

namespace BCKash.Application.Organization;

/// <summary>
/// The organisation's identity as set on the control portal's Settings → Organisation tab
/// (stored in the legacy `settings` table). Used wherever the system names itself to staff or
/// customers — email and SMS wording, the email sender name, reply-to and sign-off.
/// </summary>
public record CompanyProfile(string Name, string? Email, string? Website, string? PortalAddress, string? Address)
{
    /// <summary>Used until an administrator sets a company name.</summary>
    public const string DefaultName = "BCKash";

    public static readonly CompanyProfile Default = new(DefaultName, null, null, null, null);

    /// <summary>Plain-text sign-off for emails: the name, then whichever contact details are set.</summary>
    public string EmailFooter
    {
        get
        {
            var footer = new StringBuilder("\n\n—\n").Append(Name);
            if (!string.IsNullOrWhiteSpace(Address)) footer.Append('\n').Append(Address);
            var contact = string.Join(" · ", new[] { Email, Website }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (contact.Length > 0) footer.Append('\n').Append(contact);
            return footer.ToString();
        }
    }
}

/// <summary>The `settings` keys behind <see cref="CompanyProfile"/>.</summary>
public static class CompanyProfileKeys
{
    public const string Name = "company_name";
    public const string Email = "company_email";
    public const string Website = "company_website";
    public const string PortalAddress = "portal_address";
    public const string Address = "company_address";

    public static readonly string[] All = [Name, Email, Website, PortalAddress, Address];
}

/// <summary>What a company-profile value must look like — checked when it's saved, and again when read, so legacy junk (e.g. "http://www.") is never sent out.</summary>
public static class CompanyProfileRules
{
    public const int MaxNameLength = 100;
    public const int MaxAddressLength = 500;

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for keys outside the profile).</summary>
    public static string? Validate(string key, string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return key switch
        {
            CompanyProfileKeys.Name when trimmed.Length == 0 => "Company name is required.",
            CompanyProfileKeys.Name when trimmed.Length > MaxNameLength => $"Company name must be {MaxNameLength} characters or fewer.",
            CompanyProfileKeys.Email when trimmed.Length > 0 && !IsEmail(trimmed) => "Contact email must be a valid email address.",
            CompanyProfileKeys.Website when trimmed.Length > 0 && !IsWebAddress(trimmed) => "Website must be a full web address, e.g. https://example.com.",
            CompanyProfileKeys.PortalAddress when trimmed.Length > 0 && !IsWebAddress(trimmed) => "Customer portal address must be a full web address, e.g. https://portal.example.com.",
            CompanyProfileKeys.Address when trimmed.Length > MaxAddressLength => $"Head office address must be {MaxAddressLength} characters or fewer.",
            _ => null,
        };
    }

    public static bool IsEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return address.Address == value && address.Host.Contains('.') && !address.Host.EndsWith('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsWebAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.Host.Contains('.')
        && !uri.Host.EndsWith('.')
        && !uri.Host.StartsWith('.');
}

/// <summary>Reads the current <see cref="CompanyProfile"/>. Not cached — a saved change applies to the very next message.</summary>
public interface ICompanyProfileProvider
{
    Task<CompanyProfile> GetAsync(CancellationToken cancellationToken = default);
}
