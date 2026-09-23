using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Domain.Tests.Savings;

public class SavingsTransitionRulesTests
{
    [Theory]
    [InlineData(SavingsAccountStatus.Pending, true)]
    [InlineData(SavingsAccountStatus.Approved, false)]
    [InlineData(SavingsAccountStatus.Closed, false)]
    public void CanApprove_only_from_pending(SavingsAccountStatus status, bool expected) =>
        Assert.Equal(expected, SavingsTransitionRules.CanApprove(status));

    [Theory]
    [InlineData(SavingsAccountStatus.Pending, true)]
    [InlineData(SavingsAccountStatus.Approved, false)]
    public void CanDecline_only_from_pending(SavingsAccountStatus status, bool expected) =>
        Assert.Equal(expected, SavingsTransitionRules.CanDecline(status));

    [Theory]
    [InlineData(SavingsAccountStatus.Approved, true)]
    [InlineData(SavingsAccountStatus.Pending, false)]
    [InlineData(SavingsAccountStatus.Closed, false)]
    public void CanClose_only_from_approved(SavingsAccountStatus status, bool expected) =>
        Assert.Equal(expected, SavingsTransitionRules.CanClose(status));

    [Theory]
    [InlineData(SavingsAccountStatus.Approved, true)]
    [InlineData(SavingsAccountStatus.Pending, false)]
    public void CanTransact_only_when_approved(SavingsAccountStatus status, bool expected) =>
        Assert.Equal(expected, SavingsTransitionRules.CanTransact(status));
}
