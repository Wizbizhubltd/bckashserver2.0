# Interest & Amortization Calculation Spec

| | |
|---|---|
| **Status** | Best-effort, **NOT validated against legacy** |
| **Resolves** | FR-LN-15 in form, not in substance — see the warning below |
| **Implementation** | `BCKash.Domain/Loans/LoanScheduleGenerator.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/Loans/LoanScheduleGeneratorTests.cs` |

## ⚠️ What this document is, and isn't

FR-LN-15 calls for the legacy interest/amortization algorithm to be **extracted from BCKash's legacy PHP codebase** and validated against real historical loans before any schedule-generation code is written. That legacy source was never available in this environment, and guessing it silently was explicitly ruled out earlier in this project.

This document is the explicitly-authorized fallback: **standard, textbook microfinance amortization mathematics**, built from the schema's own field names (`interest_method`, `armotization_method`, `interest_calculation_period_type`, `year_days`, `month_days`, `grace_on_*`) rather than reverse-engineered PHP. It is internally consistent and fully unit-tested against itself, but it has **not been checked against a single real BCKash loan**. Treat every number in this document as "what our formula produces," not "what the legacy system would have produced." If and when the legacy PHP source or real historical loan data becomes available, this spec must be re-validated and this document updated — until then, any discrepancy between this system's schedules and BCKash's real-world expectations should be resolved in favor of the real-world expectation, not this document.

## Inputs

Per loan (copied from the loan's `LoanProduct` at approval time, per `ScheduleGenerationInput`):

- `Principal` — the disbursed amount.
- `InterestRate` (a plain percentage, e.g. `12` means 12%) + `InterestRateType` (Day/Week/Month/Year) — the nominal rate.
- `LoanTerm` + `LoanTermType`, `RepaymentFrequency` + `RepaymentFrequencyType` — how many installments, and how far apart.
- `InterestMethod` (Flat / DecliningBalance) × `AmortizationMethod` (EqualInstallment / EqualPrincipal).
- `CalculationPeriodType` (Same / Daily), `YearDays` (Actual/360/364/365), `MonthDays` (Actual/30/31) — day-count conventions.
- `GraceOnPrincipal`, `GraceOnInterestCharged`, `GraceOnInterestPayment` — grace periods, in installment counts.
- `DisbursementDate`.

## Number of installments and due dates

Every duration (`LoanTerm`, `RepaymentFrequency`, and rate units) is converted to a day-count using:

| Unit | Days |
|---|---|
| Day | 1 |
| Week | 7 |
| Month | `MonthDays` (30, 31, or 30 for "Actual") |
| Year | `YearDays` (360, 364, 365, or 365 for "Actual") |

`n = round(termDays / repaymentPeriodDays)`. Due dates step forward from `DisbursementDate` by one repayment period at a time (calendar-aware: `AddMonths`/`AddYears`, not a fixed day count, so month-end drift doesn't accumulate).

## Period interest rate

```
dailyRate  = (InterestRate / 100) / daysInOneRateUnit(InterestRateType)
periodRate = dailyRate × daysInOneRepaymentPeriod
```

`CalculationPeriodType.Same` (used throughout this document) applies this one `periodRate` uniformly to every installment, regardless of that specific period's actual calendar length. `CalculationPeriodType.Daily` (implemented, not used below) instead accrues `balance × dailyRate × actualCalendarDaysInPeriod` per period — supported by the code, but not shown here since exact calendar-day figures aren't practical to hand-verify in a document like this.

## The four method combinations

Let `n` = installment count, `r` = period rate, `P` = principal, `gP`/`gIC`/`gIP` = grace-on-principal / grace-on-interest-charged / grace-on-interest-payment (in installments).

**Flat** (both amortization methods — see the note below):
```
interestBearingPeriods  = n - gIC
principalBearingPeriods = n - gP
totalInterest = P × r × interestBearingPeriods
perInstallmentInterest  = totalInterest / interestBearingPeriods
perInstallmentPrincipal = P / principalBearingPeriods
```
Every non-grace period gets the same `perInstallmentInterest` and `perInstallmentPrincipal`. **Equal Installment and Equal Principal produce an identical schedule under Flat** — this is a genuine property of flat interest (it's defined as "spread evenly"), not an implementation shortcut, and Worked Examples 1–2 demonstrate it directly.

**Declining Balance + Equal Installment** (standard annuity): after `gP` interest-only periods (principal 0, `interest = balance × r`, balance unchanged), a level installment is computed over the remaining `n − gP` periods:
```
installment = balance × r × (1+r)^(n−gP) / ((1+r)^(n−gP) − 1)
```
each period: `interest = balance × r` (0 during `gIC`); `principal = installment − interest`; `balance -= principal`.

**Declining Balance + Equal Principal**: `perInstallmentPrincipal = P / (n − gP)` (constant); each period: `interest = balance × r` (0 during `gIC`); `balance -= principal`.

**Grace-on-interest-charged** (`gIC`): interest is simply not accrued for the first `gIC` periods, in every method — those periods cost nothing in interest.

**Grace-on-interest-payment** (`gIP`): interest still accrues normally for the first `gIP` periods, but isn't due — the accrued amount is deferred as a single lump sum added to installment `gIP + 1`'s interest. Total interest across the whole schedule is unchanged; only its timing shifts (Worked Example 7 in the test suite demonstrates this).

**Rounding**: every period amount is rounded to 2 decimal places as it's computed; the last installment's principal absorbs whatever rounding remainder is left so the schedule's total principal always equals the disbursed amount exactly.

## Worked examples

All examples: `DisbursementDate = 2026-01-01`, monthly repayments, `CalculationPeriodType = Same`, `YearDays = Days365`, `MonthDays = Days30`, no grace unless stated. These exact figures are asserted in `LoanScheduleGeneratorTests.cs` — the code is the source of truth; this table is a human-readable rendering of the same test data, not computed by hand.

### 1. Flat + Equal Installment — P=12,000, 1%/month, 12 installments
Every installment: **Principal 1,000.00, Interest 120.00, Total 1,120.00**. Totals: Principal 12,000.00, Interest 1,440.00.

### 2. Flat + Equal Principal — P=5,000, 2%/month, 5 installments
Every installment: **Principal 1,000.00, Interest 100.00, Total 1,100.00**. Totals: Principal 5,000.00, Interest 500.00. (Identical shape to Example 1 — the Flat-method equivalence property.)

### 3. Declining Balance + Equal Installment — P=10,000, 1%/month, 12 installments
Level installment ≈ **888.49** every period (last period 888.47 — the rounding true-up happened to require no adjustment here, since the unrounded periods already summed to exactly 10,000.00). Principal rises from 788.49 (period 1) to 879.67 (period 12); interest falls from 100.00 to 8.80. Totals: Principal 10,000.00, Interest 661.86.

### 4. Declining Balance + Equal Principal — P=6,000, 2%/month, 6 installments
Constant principal **1,000.00** every period; interest steps down by 20.00 each period: 120.00, 100.00, 80.00, 60.00, 40.00, 20.00. Totals: Principal 6,000.00, Interest 420.00.

### 5. Declining Balance + Equal Installment, with GraceOnPrincipal=2 — P=10,000, 1%/month, 12 installments
Periods 1–2: interest-only, **Principal 0.00, Interest 100.00**. Periods 3–12: level installment ≈ **1,055.82** (re-computed over the remaining 10 periods against the still-full 10,000 balance), principal rising 955.82 → 1,045.37, interest falling 100.00 → 10.45. Totals: Principal 10,000.00, Interest 758.20 (more than Example 3's 661.86, since two periods of interest-only accrual are added on top of the same 10-period amortization tail).

## Explicitly not covered here

Repayment allocation order (FR-LN-16), reversal (FR-LN-18), overpayment (FR-LN-19), waivers (FR-LN-20), reschedule regeneration (FR-LN-23), write-off (FR-LN-24), and NPA flagging (FR-LN-25) are documented in `FRD.md` next to their own requirements and implemented in `BCKash.Application/Loans`/`BCKash.Infrastructure/Loans` — this document only covers schedule generation itself.
