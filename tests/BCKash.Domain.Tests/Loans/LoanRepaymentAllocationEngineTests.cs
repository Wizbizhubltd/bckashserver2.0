using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanRepaymentAllocationEngineTests
{
    private static readonly OutstandingInstallment Installment = new(
        ScheduleId: 1, DueDate: new DateOnly(2026, 1, 1),
        PrincipalOutstanding: 1000m, InterestOutstanding: 100m, FeesOutstanding: 50m, PenaltyOutstanding: 25m);

    [Fact]
    public void InterestPrincipalPenaltyFees_pays_in_that_order()
    {
        var result = LoanRepaymentAllocationEngine.Allocate([Installment], 200m, LoanTransactionStrategy.InterestPrincipalPenaltyFees);

        var allocation = Assert.Single(result.Allocations);
        Assert.Equal(100m, allocation.Interest); // fully paid first
        Assert.Equal(100m, allocation.Principal); // remainder goes to principal
        Assert.Equal(0m, allocation.Penalty);
        Assert.Equal(0m, allocation.Fees);
        Assert.Equal(0m, result.Overpayment);
    }

    [Fact]
    public void PrincipalInterestPenaltyFees_pays_in_that_order()
    {
        var result = LoanRepaymentAllocationEngine.Allocate([Installment], 1050m, LoanTransactionStrategy.PrincipalInterestPenaltyFees);

        var allocation = Assert.Single(result.Allocations);
        Assert.Equal(1000m, allocation.Principal); // fully paid first
        Assert.Equal(50m, allocation.Interest); // remainder goes to interest
        Assert.Equal(0m, allocation.Penalty);
        Assert.Equal(0m, allocation.Fees);
    }

    [Fact]
    public void PenaltyFeesInterestPrincipal_pays_in_that_order()
    {
        var result = LoanRepaymentAllocationEngine.Allocate([Installment], 100m, LoanTransactionStrategy.PenaltyFeesInterestPrincipal);

        var allocation = Assert.Single(result.Allocations);
        Assert.Equal(25m, allocation.Penalty); // fully paid first
        Assert.Equal(50m, allocation.Fees); // then fees
        Assert.Equal(25m, allocation.Interest); // remainder to interest
        Assert.Equal(0m, allocation.Principal);
    }

    [Fact]
    public void Payment_exceeding_total_outstanding_across_all_installments_is_reported_as_overpayment()
    {
        var result = LoanRepaymentAllocationEngine.Allocate([Installment], 2000m, LoanTransactionStrategy.InterestPrincipalPenaltyFees);

        var allocation = Assert.Single(result.Allocations);
        Assert.Equal(1000m, allocation.Principal);
        Assert.Equal(100m, allocation.Interest);
        Assert.Equal(50m, allocation.Fees);
        Assert.Equal(25m, allocation.Penalty);
        Assert.Equal(825m, result.Overpayment); // 2000 - (1000+100+50+25)
    }

    [Fact]
    public void Multiple_installments_are_paid_oldest_due_date_first()
    {
        var older = Installment with { ScheduleId = 1, DueDate = new DateOnly(2026, 1, 1) };
        var newer = Installment with { ScheduleId = 2, DueDate = new DateOnly(2026, 2, 1) };

        // Enough to fully cover the older installment (1175) plus a partial payment on the newer one.
        var result = LoanRepaymentAllocationEngine.Allocate([newer, older], 1275m, LoanTransactionStrategy.InterestPrincipalPenaltyFees);

        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(1, result.Allocations[0].ScheduleId); // older installment allocated first despite input order
        Assert.Equal(100m, result.Allocations[0].Interest + 0m);
        Assert.Equal(1175m, result.Allocations[0].Principal + result.Allocations[0].Interest + result.Allocations[0].Fees + result.Allocations[0].Penalty);
        Assert.Equal(2, result.Allocations[1].ScheduleId);
        Assert.Equal(100m, result.Allocations[1].Principal + result.Allocations[1].Interest + result.Allocations[1].Fees + result.Allocations[1].Penalty);
    }
}
