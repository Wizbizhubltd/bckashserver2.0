using BCKash.Domain.Groups;
using Xunit;

namespace BCKash.Domain.Tests.Groups;

public class GroupStatusTransitionRulesTests
{
    private static readonly GroupStatus[] AllStatuses =
    [
        GroupStatus.Pending, GroupStatus.Active, GroupStatus.Inactive, GroupStatus.Declined, GroupStatus.Closed,
    ];

    [Theory]
    [InlineData(GroupStatus.Pending, true)]
    [InlineData(GroupStatus.Active, false)]
    [InlineData(GroupStatus.Inactive, false)]
    [InlineData(GroupStatus.Declined, false)]
    [InlineData(GroupStatus.Closed, false)]
    public void CanActivate_only_from_pending(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.CanActivate(status));
    }

    [Theory]
    [InlineData(GroupStatus.Pending, false)]
    [InlineData(GroupStatus.Active, true)]
    [InlineData(GroupStatus.Inactive, false)]
    [InlineData(GroupStatus.Declined, false)]
    [InlineData(GroupStatus.Closed, false)]
    public void CanDeactivate_only_from_active(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.CanDeactivate(status));
    }

    [Theory]
    [InlineData(GroupStatus.Pending, false)]
    [InlineData(GroupStatus.Active, false)]
    [InlineData(GroupStatus.Inactive, true)]
    [InlineData(GroupStatus.Declined, false)]
    [InlineData(GroupStatus.Closed, false)]
    public void CanReactivate_only_from_inactive(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.CanReactivate(status));
    }

    [Theory]
    [InlineData(GroupStatus.Pending, true)]
    [InlineData(GroupStatus.Active, false)]
    [InlineData(GroupStatus.Inactive, false)]
    [InlineData(GroupStatus.Declined, false)]
    [InlineData(GroupStatus.Closed, false)]
    public void CanDecline_only_from_pending(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.CanDecline(status));
    }

    [Theory]
    [InlineData(GroupStatus.Pending, true)]
    [InlineData(GroupStatus.Active, true)]
    [InlineData(GroupStatus.Inactive, true)]
    [InlineData(GroupStatus.Declined, false)]
    [InlineData(GroupStatus.Closed, false)]
    public void CanClose_from_pending_active_or_inactive_but_not_declined_or_closed(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.CanClose(status));
    }

    [Fact]
    public void Exactly_one_predecessor_status_can_activate_deactivate_reactivate_or_decline()
    {
        Assert.Single(AllStatuses, s => GroupStatusTransitionRules.CanActivate(s));
        Assert.Single(AllStatuses, s => GroupStatusTransitionRules.CanDeactivate(s));
        Assert.Single(AllStatuses, s => GroupStatusTransitionRules.CanReactivate(s));
        Assert.Single(AllStatuses, s => GroupStatusTransitionRules.CanDecline(s));
    }

    [Theory]
    [InlineData(GroupStatus.Declined, true)]
    [InlineData(GroupStatus.Closed, true)]
    [InlineData(GroupStatus.Pending, false)]
    [InlineData(GroupStatus.Active, false)]
    [InlineData(GroupStatus.Inactive, false)]
    public void IsTerminal_only_for_declined_or_closed(GroupStatus status, bool expected)
    {
        Assert.Equal(expected, GroupStatusTransitionRules.IsTerminal(status));
    }
}
