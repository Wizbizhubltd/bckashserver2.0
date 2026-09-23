namespace BCKash.Domain.Groups;

/// <summary>
/// The group status state machine — FR-GRP-1: "Group status lifecycle mirrors client status
/// (`pending → active`, `active ⇄ inactive`, `→ declined`, `→ closed`), each transition logged."
/// Identical rules to <see cref="Clients.ClientStatusTransitionRules"/> against the distinct
/// <see cref="GroupStatus"/> enum — see that type for the rationale behind Close's predecessor set.
/// </summary>
public static class GroupStatusTransitionRules
{
    public static bool CanActivate(GroupStatus current) => current == GroupStatus.Pending;

    public static bool CanDeactivate(GroupStatus current) => current == GroupStatus.Active;

    public static bool CanReactivate(GroupStatus current) => current == GroupStatus.Inactive;

    public static bool CanDecline(GroupStatus current) => current == GroupStatus.Pending;

    public static bool CanClose(GroupStatus current) =>
        current is GroupStatus.Pending or GroupStatus.Active or GroupStatus.Inactive;

    public static bool IsTerminal(GroupStatus status) =>
        status is GroupStatus.Declined or GroupStatus.Closed;
}
