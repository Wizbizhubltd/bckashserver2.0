namespace BCKash.Domain.Loans;

public enum LoanRepaymentComponent
{
    Principal,
    Interest,
    Fees,
    Penalty,
}

/// <summary>Outstanding-per-component amounts for one unpaid schedule line (due minus waived minus written-off minus already-paid).</summary>
public record OutstandingInstallment(int ScheduleId, DateOnly DueDate, decimal PrincipalOutstanding, decimal InterestOutstanding, decimal FeesOutstanding, decimal PenaltyOutstanding);

public record ComponentAllocation(int ScheduleId, decimal Principal, decimal Interest, decimal Fees, decimal Penalty);

public record AllocationResult(IReadOnlyList<ComponentAllocation> Allocations, decimal Overpayment);

/// <summary>
/// FR-LN-16's repayment allocation: pays down unpaid installments oldest-due-first, and within
/// each installment pays components in the loan product's configured
/// <see cref="LoanTransactionStrategy"/> order, until the installment is covered or the payment
/// is exhausted. Pure function — no I/O — so it's directly unit-testable per strategy.
/// </summary>
public static class LoanRepaymentAllocationEngine
{
    public static IReadOnlyList<LoanRepaymentComponent> ComponentOrder(LoanTransactionStrategy strategy) => strategy switch
    {
        LoanTransactionStrategy.PenaltyFeesInterestPrincipal =>
            [LoanRepaymentComponent.Penalty, LoanRepaymentComponent.Fees, LoanRepaymentComponent.Interest, LoanRepaymentComponent.Principal],
        LoanTransactionStrategy.PrincipalInterestPenaltyFees =>
            [LoanRepaymentComponent.Principal, LoanRepaymentComponent.Interest, LoanRepaymentComponent.Penalty, LoanRepaymentComponent.Fees],
        LoanTransactionStrategy.InterestPrincipalPenaltyFees =>
            [LoanRepaymentComponent.Interest, LoanRepaymentComponent.Principal, LoanRepaymentComponent.Penalty, LoanRepaymentComponent.Fees],
        _ => [LoanRepaymentComponent.Interest, LoanRepaymentComponent.Principal, LoanRepaymentComponent.Penalty, LoanRepaymentComponent.Fees],
    };

    /// <summary>Any amount left over after every installment is fully covered is FR-LN-19's overpayment — recorded on the transaction, not auto-applied to future installments (see LoanRepaymentService's doc comment for why).</summary>
    public static AllocationResult Allocate(IReadOnlyList<OutstandingInstallment> installments, decimal paymentAmount, LoanTransactionStrategy strategy)
    {
        var order = ComponentOrder(strategy);
        var remaining = paymentAmount;
        var allocations = new List<ComponentAllocation>();

        foreach (var installment in installments.OrderBy(i => i.DueDate).ThenBy(i => i.ScheduleId))
        {
            if (remaining <= 0)
            {
                break;
            }

            var outstanding = new Dictionary<LoanRepaymentComponent, decimal>
            {
                [LoanRepaymentComponent.Principal] = installment.PrincipalOutstanding,
                [LoanRepaymentComponent.Interest] = installment.InterestOutstanding,
                [LoanRepaymentComponent.Fees] = installment.FeesOutstanding,
                [LoanRepaymentComponent.Penalty] = installment.PenaltyOutstanding,
            };
            var paid = new Dictionary<LoanRepaymentComponent, decimal>
            {
                [LoanRepaymentComponent.Principal] = 0m,
                [LoanRepaymentComponent.Interest] = 0m,
                [LoanRepaymentComponent.Fees] = 0m,
                [LoanRepaymentComponent.Penalty] = 0m,
            };

            foreach (var component in order)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var due = outstanding[component];
                if (due <= 0)
                {
                    continue;
                }

                var pay = Math.Min(due, remaining);
                paid[component] = pay;
                remaining -= pay;
            }

            if (paid.Values.Any(v => v > 0))
            {
                allocations.Add(new ComponentAllocation(
                    installment.ScheduleId,
                    paid[LoanRepaymentComponent.Principal],
                    paid[LoanRepaymentComponent.Interest],
                    paid[LoanRepaymentComponent.Fees],
                    paid[LoanRepaymentComponent.Penalty]));
            }
        }

        return new AllocationResult(allocations, Math.Max(0m, remaining));
    }
}
