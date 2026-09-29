using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Infrastructure;

/// <summary>
/// The names behind a set of loans' or applications' ids — client or group, product and office —
/// loaded in one pass so lists show who and what instead of bare ids.
/// </summary>
public sealed class LoanDisplayNames
{
    private readonly Dictionary<int, string> _clients;
    private readonly Dictionary<int, string> _groups;
    private readonly Dictionary<int, string> _products;
    private readonly Dictionary<int, string> _offices;

    private LoanDisplayNames(Dictionary<int, string> clients, Dictionary<int, string> groups, Dictionary<int, string> products, Dictionary<int, string> offices)
    {
        _clients = clients;
        _groups = groups;
        _products = products;
        _offices = offices;
    }

    public static async Task<LoanDisplayNames> LoadAsync(
        BCKashDbContext db,
        IEnumerable<int?> clientIds,
        IEnumerable<int?> groupIds,
        IEnumerable<int?> productIds,
        IEnumerable<int?> officeIds,
        CancellationToken cancellationToken)
    {
        static List<int> Ids(IEnumerable<int?> ids) => ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();

        var clientList = Ids(clientIds);
        var groupList = Ids(groupIds);
        var productList = Ids(productIds);
        var officeList = Ids(officeIds);

        var clients = (await db.Clients
                .Where(c => clientList.Contains(c.Id))
                .Select(c => new { c.Id, c.DisplayName, c.FirstName, c.LastName })
                .ToListAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => !string.IsNullOrWhiteSpace(c.DisplayName) ? c.DisplayName! : $"{c.FirstName} {c.LastName}".Trim());
        var groups = await db.Groups.Where(g => groupList.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name ?? $"Group #{g.Id}", cancellationToken);
        var products = await db.LoanProducts.Where(p => productList.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name ?? $"Product #{p.Id}", cancellationToken);
        var offices = await db.Offices.Where(o => officeList.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name ?? $"Office #{o.Id}", cancellationToken);
        return new LoanDisplayNames(clients, groups, products, offices);
    }

    /// <summary>The client's name for a client loan, else the group's.</summary>
    public string? Applicant(int? clientId, int? groupId) =>
        clientId is { } c && _clients.TryGetValue(c, out var client) ? client
        : groupId is { } g && _groups.TryGetValue(g, out var group) ? group
        : null;

    public string? Product(int? productId) => productId is { } p ? _products.GetValueOrDefault(p) : null;

    public string? Office(int? officeId) => officeId is { } o ? _offices.GetValueOrDefault(o) : null;
}
