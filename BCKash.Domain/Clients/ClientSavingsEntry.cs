using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

public enum ClientSavingsEntryType
{
    /// <summary>The savings share of a loan repayment.</summary>
    Contribution,

    /// <summary>A reversed repayment takes its savings share back out.</summary>
    ContributionReversal,

    /// <summary>The savings paid out to the client.</summary>
    Withdrawal,

    /// <summary>The 15% kept when a client cashes out while a loan is still running.</summary>
    EarlyWithdrawalFee,

    /// <summary>Everything saved, lost when one of the client's loans is written off.</summary>
    Forfeiture,
}

/// <summary>
/// One movement on a client's loan savings — the share of every repayment set aside for them. The
/// balance is the sum of all their entries and carries across loans. New — no legacy table.
/// </summary>
public class ClientSavingsEntry : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int? LoanId { get; set; }

    /// <summary>The repayment a contribution (or its reversal) came from.</summary>
    public int? LoanTransactionId { get; set; }

    public Loans.LoanTransaction? LoanTransaction { get; set; }

    public ClientSavingsEntryType Type { get; set; }

    /// <summary>Positive adds to the savings; negative takes away.</summary>
    public decimal Amount { get; set; }

    public string? Notes { get; set; }
    public int? CreatedById { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Client loan savings: a share of every repayment on a client's loan goes into their savings instead of
/// the loan. So the loan still clears on schedule, what the client pays is grossed up — each instalment's
/// dues ÷ (1 − rate) — and the savings share of that is exactly the top-up. The savings carry across loans.
/// They can be withdrawn in full once no loan is running; cashing out while one is costs a charge; writing a
/// loan off forfeits them. Both percentages are settings (Settings → Loan → Client savings).
/// </summary>
public static class ClientSavingsRules
{
    /// <summary>The savings share of a repayment.</summary>
    public static decimal SavingsShare(decimal repayment, decimal rate) => Math.Round(repayment * rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>What the client pays so that, after the savings share, <paramref name="due"/> reaches the loan.</summary>
    public static decimal GrossUp(decimal due, decimal? rate) =>
        rate is > 0 and < 1 ? Math.Round(due / (1 - rate.Value), 2, MidpointRounding.AwayFromZero) : due;

    /// <summary>What an early cash-out keeps back, at <paramref name="feeRate"/> (0.15 = 15%).</summary>
    public static decimal EarlyWithdrawalFee(decimal balance, decimal feeRate) => Math.Round(balance * feeRate, 2, MidpointRounding.AwayFromZero);
}
