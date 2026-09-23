using BCKash.Domain.Identity;
using Xunit;

namespace BCKash.Domain.Tests.Identity;

public class UserOnboardingRulesTests
{
    [Theory]
    [InlineData(UserClass.Initiator, true)]
    [InlineData(UserClass.Authorizer, false)]
    [InlineData(UserClass.Reviewer, false)]
    [InlineData(null, false)]
    public void Only_initiator_can_initiate(UserClass? actingClass, bool expected)
    {
        Assert.Equal(expected, UserOnboardingRules.CanInitiate(actingClass));
    }

    [Fact]
    public void Authorizer_of_the_same_user_type_can_authorize()
    {
        Assert.True(UserOnboardingRules.CanAuthorize(UserClass.Authorizer, "manager", "manager"));
    }

    [Fact]
    public void Authorizer_of_a_different_user_type_cannot_authorize()
    {
        Assert.False(UserOnboardingRules.CanAuthorize(UserClass.Authorizer, "director", "manager"));
    }

    [Theory]
    [InlineData(UserClass.Initiator)]
    [InlineData(UserClass.Reviewer)]
    [InlineData(null)]
    public void Only_the_authorizer_class_can_authorize_regardless_of_user_type_match(UserClass? actingClass)
    {
        Assert.False(UserOnboardingRules.CanAuthorize(actingClass, "manager", "manager"));
    }

    [Fact]
    public void User_type_match_is_case_insensitive()
    {
        Assert.True(UserOnboardingRules.CanAuthorize(UserClass.Authorizer, "Manager", "manager"));
    }

    [Theory]
    [InlineData(UserOnboardingStatus.Pending, true)]
    [InlineData(UserOnboardingStatus.Approved, false)]
    [InlineData(UserOnboardingStatus.Declined, false)]
    public void Only_pending_records_can_transition(UserOnboardingStatus status, bool expected)
    {
        Assert.Equal(expected, UserOnboardingRules.CanTransition(status));
    }
}
