using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanTransitionRulesTests
{
    [Theory]
    [InlineData(LoanStatus.Pending, true)]
    [InlineData(LoanStatus.NeedChanges, false)]
    [InlineData(LoanStatus.Approved, false)]
    [InlineData(LoanStatus.Disbursed, false)]
    [InlineData(LoanStatus.New, false)]
    public void CanRequestChanges_only_from_pending(LoanStatus status, bool expected)
    {
        Assert.Equal(expected, LoanTransitionRules.CanRequestChanges(status));
    }

    [Theory]
    [InlineData(LoanStatus.NeedChanges, true)]
    [InlineData(LoanStatus.Pending, false)]
    [InlineData(LoanStatus.Approved, false)]
    [InlineData(LoanStatus.Disbursed, false)]
    public void CanResubmit_only_from_need_changes(LoanStatus status, bool expected)
    {
        Assert.Equal(expected, LoanTransitionRules.CanResubmit(status));
    }

    [Theory]
    [InlineData(LoanStatus.Pending, true)]
    [InlineData(LoanStatus.NeedChanges, false)]
    [InlineData(LoanStatus.Approved, false)]
    [InlineData(LoanStatus.Disbursed, false)]
    [InlineData(LoanStatus.New, false)]
    public void CanDisburse_only_from_pending(LoanStatus status, bool expected)
    {
        Assert.Equal(expected, LoanTransitionRules.CanDisburse(status));
    }
}
