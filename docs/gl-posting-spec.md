# GL Posting Spec — Loan Transactions

| | |
|---|---|
| **Status** | Best-effort, **NOT validated against legacy** |
| **Resolves** | FR-GL-2/BR-GL-2 in form, not in substance — see the warning below |
| **Implementation** | `BCKash.Domain/GeneralLedger/LoanGlPostingRules.cs`, `BCKash.Infrastructure/Loans/LoanGlPostingService.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/GeneralLedger/LoanGlPostingRulesTests.cs`, `tests/BCKash.Api.IntegrationTests/GeneralLedger/LoanGlPostingEndToEndTests.cs` |

## ⚠️ What this document is, and isn't

The **account roles** each loan transaction posts to come straight from `LoanProduct`'s twelve `GlAccount*Id` fields (Phase 4) — fund source, loan portfolio, receivable interest/fee/penalty, income interest/fee/penalty, overpayments, suspended income, written-off, recovery. Those names are unambiguous, so which *role* a posting targets isn't in question.

Which *side* (debit or credit) each role gets hit on, for each transaction type, is not visible anywhere in the schema, and no legacy PHP source was ever available to extract it from — the same situation as [`interest-calculation-spec.md`](interest-calculation-spec.md)'s FR-LN-15. This document is the explicitly-authorized fallback for that gap too: **standard MFI double-entry bookkeeping**, applied consistently and always balanced (debit total == credit total for every transaction), but **not checked against a single real BCKash GL entry**. If and when the legacy source becomes available, this spec must be re-validated — until then, treat every posting rule below as "what our engine produces," not "what BCKash's real ledger would show."

## Posting rules

All amounts below are the transaction's own component amounts (e.g. a repayment's principal/interest/fees/penalty/overpayment, already computed by `LoanRepaymentAllocationEngine`). A component amount of zero produces no line for that component. If any account a rule needs is unmapped on the product (`null`), the whole posting is skipped — no partial or unbalanced entry is ever written; see `LoanGlPostingService`'s doc comment.

### Disbursement (FR-LN-8/BR-GL-2)

| Account | Debit | Credit |
|---|---|---|
| Loan Portfolio | amount | |
| Fund Source | | amount |

**Worked example**: disburse ₦12,000 → Debit Loan Portfolio 12,000 / Credit Fund Source 12,000.

### Repayment (FR-LN-16)

| Account | Debit | Credit |
|---|---|---|
| Fund Source | total | |
| Loan Portfolio | | principal |
| Income Interest | | interest |
| Income Fee | | fees |
| Income Penalty | | penalty |
| Loan Overpayments | | overpayment |

**Worked example**: a ₦1,200 repayment covering principal 1,000 / interest 120 / fees 50 / penalty 25 / overpayment 5 → Debit Fund Source 1,200; Credit Loan Portfolio 1,000, Income Interest 120, Income Fee 50, Income Penalty 25, Loan Overpayments 5. (1,000+120+50+25+5 = 1,200 = the debit.)

### Write-off (FR-LN-24)

| Account | Debit | Credit |
|---|---|---|
| Loans Written Off | total | |
| Loan Portfolio | | principal |
| Receivable Interest | | interest |
| Receivable Fee | | fees |
| Receivable Penalty | | penalty |

**Worked example**: writing off principal 1,000 / interest 120 / fees 50 / penalty 25 → Debit Loans Written Off 1,195; Credit Loan Portfolio 1,000, Receivable Interest 120, Receivable Fee 50, Receivable Penalty 25.

### Write-off recovery (FR-LN-24)

| Account | Debit | Credit |
|---|---|---|
| Fund Source | amount | |
| Income Recovery | | amount |

**Worked example**: recovering ₦300 on a written-off loan → Debit Fund Source 300 / Credit Income Recovery 300.

### Waivers (FR-LN-20)

Interest, fee, and penalty waivers reverse an accrued-income/receivable pair. A principal waiver is booked like a miniature write-off — forgiving principal has the same balance-sheet effect as writing it off, and there's no separate "principal waiver" account role in the schema to use instead.

| Component | Debit | Credit |
|---|---|---|
| Interest | Income Interest | Receivable Interest |
| Fees | Income Fee | Receivable Fee |
| Penalty | Income Penalty | Receivable Penalty |
| Principal | Loans Written Off | Loan Portfolio |

**Worked example**: waiving ₦75 of interest → Debit Income Interest 75 / Credit Receivable Interest 75.

## What's out of scope here

- **Provisioning** (BR-LN-10's second half, `LoanProvisioningCriteria`) — no posting rule is implemented for it; it needs its own arrears-banding logic this pass doesn't build.
- **Interest/fee/penalty accrual** (`GlTransactionType.InterestAccrual`/`FeeAccrual`) — nothing in Phase 5 writes these `LoanTransaction` rows yet (per BR-LN-5's note), so there's nothing to post GL entries for. The receivable accounts referenced by the waiver/write-off rules above assume an accrual layer that doesn't actually populate them yet — a real accrual job would need to land before those receivable balances mean anything in practice.
- **Savings, payroll, fixed-asset, expense, and other-income postings** — out of this phase's scope (loan transactions only); their own `I*GlPostingService` equivalents land in their respective future phases, per the phase spec.
