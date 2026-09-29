namespace BCKash.Domain.Clients;

/// <summary>
/// A client who has ever been approved can't be deleted directly — someone raises a
/// <see cref="DeletionRequest"/> with a reason and a super admin decides. The same goes for a group
/// with an approved member.
/// </summary>
public static class ClientDeletionRules
{
    public static bool WasApproved(Client client) => client.ActivatedDate.HasValue || client.Status == ClientStatus.Active;

    public static bool CanDeleteDirectly(Client client) => !WasApproved(client);
}
