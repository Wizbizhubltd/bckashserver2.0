using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

/// <summary>
/// GL posting hook for loan transactions (BR-GL-2, FR-GL-2). Phase 5 shipped this as a
/// no-op/stub with only <see cref="PostDisbursementAsync"/> declared, by design — GL posting
/// was explicitly deferred to Phase 6. This now covers every loan transaction type Phase 5's
/// services actually write (disbursement, repayment, write-off, write-off recovery, waivers).
/// Each method loads the loan's product, builds balanced posting lines via
/// <see cref="BCKash.Domain.GeneralLedger.LoanGlPostingRules"/>, and — unless the transaction's
/// date falls on/before an active GL closure for its office, or the product isn't fully
/// configured for GL — writes one <see cref="BCKash.Domain.GeneralLedger.GlJournalEntry"/> row
/// per line, all sharing one `Reference` so they can be looked up/reversed together.
/// </summary>
public interface ILoanGlPostingService
{
    Task PostDisbursementAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default);

    Task PostRepaymentAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default);

    Task PostWriteOffAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default);

    Task PostWriteOffRecoveryAsync(Loan loan, LoanTransaction transaction, CancellationToken cancellationToken = default);

    Task PostWaiverAsync(Loan loan, LoanTransaction transaction, LoanRepaymentComponent component, decimal amount, CancellationToken cancellationToken = default);
}
