namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// One balanced-entry line produced by <see cref="LoanGlPostingRules"/> — not an EF entity,
/// just the pure-computation output the infrastructure layer turns into a
/// <see cref="GlJournalEntry"/> row.
/// </summary>
public record GlPostingLine(int GlAccountId, decimal? Debit, decimal? Credit, GlTransactionSubType? SubType = null);
