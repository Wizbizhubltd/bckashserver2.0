namespace BCKash.Domain.Clients;

/// <summary>
/// The client status state machine (FR-CLI-2, BR-CLI-2): "pending → active (activation,
/// requires activation date and activator), active ⇄ inactive (with reason), → declined
/// (with reason, from pending), → closed (with reason, terminal). Each transition records
/// the responsible user and date. Reactivation from inactive is supported."
///
/// Close is reachable from Pending, Active, or Inactive — any status that once represented
/// a live client relationship — but not from Declined, which is its own terminal path for a
/// client that was never approved. [ENGINEERING DECISION, documented in FRD.md — FR-CLI-2
/// doesn't enumerate Close's valid predecessors explicitly.]
/// </summary>
public static class ClientStatusTransitionRules
{
    public static bool CanActivate(ClientStatus current) => current == ClientStatus.Pending;

    public static bool CanDeactivate(ClientStatus current) => current == ClientStatus.Active;

    public static bool CanReactivate(ClientStatus current) => current == ClientStatus.Inactive;

    public static bool CanDecline(ClientStatus current) => current == ClientStatus.Pending;

    public static bool CanClose(ClientStatus current) =>
        current is ClientStatus.Pending or ClientStatus.Active or ClientStatus.Inactive;

    public static bool IsTerminal(ClientStatus status) =>
        status is ClientStatus.Declined or ClientStatus.Closed;
}
