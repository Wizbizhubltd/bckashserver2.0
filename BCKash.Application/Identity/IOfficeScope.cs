namespace BCKash.Application.Identity;

/// <summary>
/// The offices the signed-in user may see and work in. A super admin is unrestricted; a director
/// covers every office in the zones assigned to them; a controller, manager or marketer covers only
/// the one office on their user record (none if they have no office yet). A user with no user_type
/// is unrestricted, as before scoping existed — neither portal lets them sign in.
/// </summary>
public interface IOfficeScope
{
    /// <summary>The caller's user_type slug, or null if they have none.</summary>
    Task<string?> GetUserTypeAsync(CancellationToken cancellationToken = default);

    /// <summary>Null when unrestricted (super admin); otherwise the exact set of office ids, possibly empty.</summary>
    Task<IReadOnlyCollection<int>?> GetOfficeIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the caller may work with a record in <paramref name="officeId"/>. A record with no
    /// office is only reachable by an unrestricted caller.
    /// </summary>
    Task<bool> CanAccessOfficeAsync(int? officeId, CancellationToken cancellationToken = default);
}
