namespace BCKash.Domain.Identity;

public record OfficePortalModule(string Slug, string Name, string Description);

/// <summary>
/// The office-portal modules a super admin can tick per staff role (see <see cref="RoleModule"/>).
/// A module decides what a role sees and can open in the office portal; role permissions still
/// decide which actions the API accepts inside it.
/// </summary>
public static class OfficePortalModules
{
    public const string Offices = "offices";
    public const string Staff = "staff";
    public const string Clients = "clients";
    public const string Loans = "loans";

    public static readonly IReadOnlyList<OfficePortalModule> All =
    [
        new(Offices, "Zones & offices", "See the zones and offices the user oversees, with their staff and figures."),
        new(Staff, "Staff", "View, onboard and manage staff ranked below the user in their office(s)."),
        new(Clients, "Clients", "Clients and groups — register, edit and activate them."),
        new(Loans, "Loans", "Loan applications and the loans they become."),
    ];

    public static readonly IReadOnlySet<string> Slugs = All.Select(m => m.Slug).ToHashSet();

    /// <summary>
    /// What each role starts with: a marketer works clients and loans; a manager and a controller
    /// also manage staff; a director additionally oversees the offices in their zones. Super admins
    /// use the control portal, so they have none.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Defaults = new Dictionary<string, string[]>
    {
        [UserTypeSlugs.Director] = [Offices, Staff, Clients, Loans],
        [UserTypeSlugs.Controller] = [Staff, Clients, Loans],
        [UserTypeSlugs.Manager] = [Staff, Clients, Loans],
        [UserTypeSlugs.Marketer] = [Clients, Loans],
    };
}
