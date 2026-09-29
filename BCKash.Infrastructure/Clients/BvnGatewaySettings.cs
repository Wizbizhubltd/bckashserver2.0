namespace BCKash.Infrastructure.Clients;

/// <summary>
/// The BC Kash MFB core gateway used for BVN lookups — set via BvnGateway__BaseUrl,
/// BvnGateway__Email and BvnGateway__Password in .env.
/// </summary>
public class BvnGatewaySettings
{
    public const string SectionName = "BvnGateway";

    public string BaseUrl { get; set; } = "https://core.bckashmfb.com/v1";
    public string? Email { get; set; }
    public string? Password { get; set; }
}
