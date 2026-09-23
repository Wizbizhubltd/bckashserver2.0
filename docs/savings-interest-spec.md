# Savings Interest & GL Posting Spec

| | |
|---|---|
| **Status** | Directly per FR-SAV-4's spec (interest math); best-effort, **NOT validated against legacy** (GL posting roles) |
| **Implementation** | `BCKash.Domain/Savings/SavingsInterestCalculator.cs`, `BCKash.Domain/GeneralLedger/SavingsGlPostingRules.cs`, `BCKash.Infrastructure/Savings/SavingsInterestPostingService.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/Savings/SavingsInterestCalculatorTests.cs`, `tests/BCKash.Domain.Tests/GeneralLedger/SavingsGlPostingRulesTests.cs`, `tests/BCKash.Api.IntegrationTests/Savings/SavingsInterestPostingTests.cs` |

## Interest calculation (FR-SAV-4)

Unlike the loan interest formula (FR-LN-15), FR-SAV-4 directly specifies both calculation types, the compounding/posting periods, and the 360/365 day-count convention — there was no "extract from legacy first" instruction blocking this, so it's implemented straight from the spec rather than escalated as a best-effort guess. The exact rounding/period-boundary behavior isn't confirmed against a legacy sample, but the formulas themselves aren't guesswork.

- **Daily balance**: `Σ (balance_that_day × annualRate / yearDays)` — a time-weighted sum over every day since the account's last calculation date, built by replaying the account's transactions chronologically (`SavingsInterestPostingService.BuildDailyBalancesAsync`).
- **Average balance**: `((openingBalance + closingBalance) / 2) × annualRate / yearDays × daysInPeriod` — the standard two-point simplification, using the period's opening balance (the balance in effect on the last calculation date) and closing balance (the balance as of today).

**These two methods produce mathematically identical totals when the balance is constant across the period** — a genuine, testable equivalence property, the same kind Phase 5's Flat-interest methods had — and **genuinely differ whenever the balance changes mid-period**, since the average-balance method doesn't account for exactly when within the period a deposit or withdrawal landed.

### Worked example 1 — constant balance (both methods agree)

₦10,000 held for 31 days, 7.2%/year, 360-day convention (daily rate = 0.0002):

- Daily balance: `10000 × 0.0002 × 31 = 62.00`
- Average balance: `((10000+10000)/2) × 0.0002 × 31 = 62.00`

### Worked example 2 — a deposit lands mid-period (methods differ)

₦10,000 held for 10 days, then a ₦5,000 deposit brings it to ₦15,000 for the remaining 20 days of a 30-day period:

- Daily balance: `(10×10000 + 20×15000) × 0.0002 = (100000 + 300000) × 0.0002 = 80.00`
- Average balance: `((10000+15000)/2) × 0.0002 × 30 = 12500 × 0.0002 × 30 = 75.00`

## The accrual/posting split

Interest **accrues** on the product's compounding period (`InterestCompoundingPeriod`) — computed and added to `SavingsAccount.InterestEarned`, not yet paid out — and **posts** (becomes a real, balance-increasing `SavingsTransaction`) on the product's posting period (`InterestPostingPeriod`), which is typically the same as or longer than the compounding period. `InterestPosted` tracks how much of `InterestEarned` has actually been posted; each posting run pays out the difference.

## No job scheduler

There is no job scheduler anywhere in this codebase (the same situation Phase 5's NPA recompute hit). `ISavingsInterestPostingService.RunDueAsync` is exposed via `POST /api/savings-interest/run`, safe to call as often as desired — each account only accrues/posts when its own `NextInterestCalculationDate`/`NextInterestPostingDate` has actually arrived, so repeat calls are no-ops for accounts that aren't due yet.

## GL posting (BR-GL-2 extended to savings, Phase 7)

`SavingsProduct` has two GL fields whose exact roles have no legacy source to confirm (the same situation as every GL-posting decision since Phase 6 — no legacy PHP source was ever available):

- **`GlAccountSavingsReferenceId`** is read as the cash/fund-side account — debited on deposit, credited on withdrawal.
- **`GlAccountSavingsControlId`** is read as the depositor-liability control account — credited on deposit, debited on withdrawal.

This is the standard Fineract-style convention for these exact field names, and the only reading that gives every transaction type (deposit, withdrawal, interest, charges) a coherent, balanced posting. If a legacy source becomes available, this must be re-validated.

| Transaction | Debit | Credit |
|---|---|---|
| Deposit | Savings Reference | Savings Control |
| Withdrawal | Savings Control | Savings Reference |
| Interest posting | Interest On Savings (expense) | Savings Control |
| Fee/penalty charge | Savings Control | Income Fee / Income Penalty |

### Loan↔savings transfers (FR-SAV-6)

A loan repayment funded from savings posts **one** balanced batch — Debit Savings Control / Credit the loan's normal repayment lines (Loan Portfolio, Income Interest, Income Fee, Income Penalty) — instead of the usual Debit Fund Source, since no cash actually moves between the institution and an external account. See `LoanGlPostingRules.ForRepaymentFromSavings`.

A loan disbursement deposited into savings posts Debit the loan product's Fund Source / Credit Savings Control — the same shape as an ordinary deposit, just funded from the loan's fund account instead of external cash.

## What's out of scope here

- **Overdraft interest** — charging interest on a negative (overdrawn) balance isn't implemented; `GlAccountOverdraftPortfolioId` and `GlAccountIncomeInterestId` (savings-side income interest, distinct from the expense-side `InterestOnSavings`) remain mapped-but-unused, the same "not everything a schema names is built yet" treatment Phase 6 gave `GlAccountSuspendedIncomeId`.
- **Savings written-off** (`GlAccountSavingsWrittenOffId`) — no write-off workflow for a savings account exists.
