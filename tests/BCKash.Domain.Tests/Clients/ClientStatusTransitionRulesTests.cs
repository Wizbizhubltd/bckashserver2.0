using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Domain.Tests.Clients;

public class ClientStatusTransitionRulesTests
{
    private static readonly ClientStatus[] AllStatuses =
    [
        ClientStatus.Pending, ClientStatus.Active, ClientStatus.Inactive, ClientStatus.Declined, ClientStatus.Closed,
    ];

    [Theory]
    [InlineData(ClientStatus.Pending, true)]
    [InlineData(ClientStatus.Active, false)]
    [InlineData(ClientStatus.Inactive, false)]
    [InlineData(ClientStatus.Declined, false)]
    [InlineData(ClientStatus.Closed, false)]
    public void CanActivate_only_from_pending(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.CanActivate(status));
    }

    [Theory]
    [InlineData(ClientStatus.Pending, false)]
    [InlineData(ClientStatus.Active, true)]
    [InlineData(ClientStatus.Inactive, false)]
    [InlineData(ClientStatus.Declined, false)]
    [InlineData(ClientStatus.Closed, false)]
    public void CanDeactivate_only_from_active(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.CanDeactivate(status));
    }

    [Theory]
    [InlineData(ClientStatus.Pending, false)]
    [InlineData(ClientStatus.Active, false)]
    [InlineData(ClientStatus.Inactive, true)]
    [InlineData(ClientStatus.Declined, false)]
    [InlineData(ClientStatus.Closed, false)]
    public void CanReactivate_only_from_inactive(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.CanReactivate(status));
    }

    [Theory]
    [InlineData(ClientStatus.Pending, true)]
    [InlineData(ClientStatus.Active, false)]
    [InlineData(ClientStatus.Inactive, false)]
    [InlineData(ClientStatus.Declined, false)]
    [InlineData(ClientStatus.Closed, false)]
    public void CanDecline_only_from_pending(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.CanDecline(status));
    }

    [Theory]
    [InlineData(ClientStatus.Pending, true)]
    [InlineData(ClientStatus.Active, true)]
    [InlineData(ClientStatus.Inactive, true)]
    [InlineData(ClientStatus.Declined, false)]
    [InlineData(ClientStatus.Closed, false)]
    public void CanClose_from_pending_active_or_inactive_but_not_declined_or_closed(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.CanClose(status));
    }

    [Fact]
    public void Exactly_one_predecessor_status_can_activate_deactivate_reactivate_or_decline()
    {
        Assert.Single(AllStatuses, s => ClientStatusTransitionRules.CanActivate(s));
        Assert.Single(AllStatuses, s => ClientStatusTransitionRules.CanDeactivate(s));
        Assert.Single(AllStatuses, s => ClientStatusTransitionRules.CanReactivate(s));
        Assert.Single(AllStatuses, s => ClientStatusTransitionRules.CanDecline(s));
    }

    [Theory]
    [InlineData(ClientStatus.Declined, true)]
    [InlineData(ClientStatus.Closed, true)]
    [InlineData(ClientStatus.Pending, false)]
    [InlineData(ClientStatus.Active, false)]
    [InlineData(ClientStatus.Inactive, false)]
    public void IsTerminal_only_for_declined_or_closed(ClientStatus status, bool expected)
    {
        Assert.Equal(expected, ClientStatusTransitionRules.IsTerminal(status));
    }
}
