# Phase 8 Spec — Payroll, Fixed Assets, Expenses & Other Income

| | |
|---|---|
| **Status** | Depreciation method and payroll gross-pay entry confirmed with business; GL posting rules are best-effort, **NOT validated against legacy** (same caveat as [`gl-posting-spec.md`](gl-posting-spec.md)) |
| **Resolves** | BR-PAY-1..3/FR-PAY-1..3, BR-AST-1..2/FR-AST-1..2, BR-EXP-1..3/FR-EXP-1..3 |
| **Implementation** | `BCKash.Domain/{Payroll,Assets,Expenses,GeneralLedger}/*.cs`, `BCKash.Application/{PayrollProcessing,Assets,Expenses}/*.cs`, `BCKash.Infrastructure/{PayrollProcessing,Assets,Expenses}/*.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/{Payroll,Assets,Expenses,GeneralLedger}/*.cs`, `tests/BCKash.Api.IntegrationTests/GeneralLedger/Phase8GlPostingEndToEndTests.cs` |

## Decisions confirmed with business (previously flagged in the FRD)

- **Depreciation method (FR-AST-2): straight-line.** The FRD flagged this as unconfirmed — the legacy schema's `rate`/`cost`/`accumulated`/`ending_value` columns were consistent with straight-line but didn't rule out declining-balance. Confirmed straight-line: `(cost − salvage value) / useful life` depreciated evenly each year, until the asset reaches its salvage value. See `BCKash.Domain/Assets/AssetDepreciationRules.cs`.
- **Payroll gross pay (FR-PAY-2): manual entry per run.** `Payroll` has no stored base-salary field on `User`, and none was added — this matches the legacy schema exactly. Whoever runs payroll enters the gross amount each time; the template's line items compute net pay from it.
- **"Screens" (phase scope): API endpoints only.** This repo is API-only — no frontend project exists here. Consistent with every phase so far, "admin/finance screens" means controllers/endpoints, not a UI.

## Fixed-asset depreciation (BR-AST-2, FR-AST-2)

`AssetDepreciationRules.ComputeSchedule`/`ComputeNextYear` are pure functions (no I/O) — `IAssetDepreciationService.RunAsync` calls `ComputeNextYear` with however much has already been depreciated, so the job can run one asset, one year, at a time rather than materializing the whole schedule up front. It's idempotent per `(asset, year)` — a second call for the same year returns `AlreadyRun` rather than double-posting.

**Worked example**: an asset costing ₦12,000 with a ₦2,000 salvage value and a 5-year useful life depreciates ₦2,000/year for 5 years, landing exactly on the ₦2,000 salvage value in year 5 (the final year is truncated to land exactly on salvage rather than overshoot).

GL posting (`AssetGlPostingRules.ForDepreciation`) debits the asset type's `GlAccountExpenseId` and credits its `GlAccountContraAssetId` (accumulated depreciation):

| Account | Debit | Credit |
|---|---|---|
| Depreciation Expense | amount | |
| Accumulated Depreciation (contra-asset) | | amount |

No posting rule is implemented for asset *acquisition* — the phase's scope was "GL posting for... depreciation," not the purchase itself.

## Payroll (BR-PAY-1..3, FR-PAY-1..3)

### Net-pay computation

`PayrollComputationRules.Compute` (pure, no I/O) takes a gross amount and a snapshot of the run's line items (each copied from `PayrollTemplateMeta` onto a `PayrollMeta` row at run time, so a later template edit never changes a past run's numbers) and computes net pay in two passes:

1. **Non-tax lines** (`IsTax = false`) apply first, each against the *gross* amount — fixed lines add/subtract their value outright; percentage lines add/subtract `value%` of gross. Their running total produces an *intermediate net*.
2. **Tax lines** (`IsTax = true`) apply second, so a tax whose `TaxOn` is `Net` is computed against the intermediate net from step 1, while `TaxOn = Gross` still uses the original gross.

Every line's `Type` (Addition/Deduction) determines its sign regardless of whether it's a tax line — `IsTax` only controls what base a percentage is computed against, not the sign.

**Worked example**: gross ₦100,000, with a ₦15,000 fixed housing allowance, a 5% (of gross) transport allowance, a ₦2,000 fixed pension deduction, and a 7.5% (of net) tax deduction:
- Non-tax: 100,000 + 15,000 + 5,000 − 2,000 = 118,000 (intermediate net)
- Tax: 7.5% of 118,000 = 8,850 → **final net pay = 109,150**

No legacy PHP source was available to confirm this is the order BCKash's real system used — same "no legacy source, documented assumption" situation as [`interest-calculation-spec.md`](interest-calculation-spec.md)'s FR-LN-15 caveat.

### GL posting

Unlike Loans/Assets/Expenses, GL account roles aren't configured on a shared "product" — the legacy schema has no payroll-template-level GL configuration, so `Payroll` itself carries `GlAccountExpenseId`/`GlAccountAssetId` per run:

| Account | Debit | Credit |
|---|---|---|
| Payroll Expense (run's own) | net pay | |
| Asset/cash (run's own) | | net pay |

### Recurring payroll (FR-PAY-3)

No scheduler exists anywhere in this codebase (same situation as FR-SAV-4/FR-LN-25's notes), so recurrence is exposed as an admin-triggered endpoint (`POST /api/payroll/run-recurring`) rather than a background job. `PayrollRecurrenceRules.NextDate` reads the legacy `recur_frequency` free-text column as an interval count (e.g. `"2"` + `Weeks` = every 2 weeks), defaulting to 1 when it isn't a parseable positive integer. Each due run clones a new `Payroll` row dated on the due date — recomputed against the template's *current* line items, not a frozen copy — and advances the original's `RecurNextDate`; if that would exceed `RecurEndDate`, the original stops recurring.

## Expenses & other income (BR-EXP-1..3, FR-EXP-1..3)

Both follow the same approval-workflow shape: new records start `Pending`; `POST {id}/approve` (optional notes) or `POST {id}/decline` (reason required) are dedicated state-transition sub-resources, mirroring `LoanApplicationsController`'s approve/decline pattern. GL posting only happens on approval — a declined or still-pending record never posts. Both entities were given `IAuditable` in this phase (previously only `IHasTimestamps`) so approve/decline actions get audit-trail coverage per FR-SEC-6, consistent with `Group`'s Phase 3 precedent.

### Expense approval

| Account | Debit | Credit |
|---|---|---|
| Expense (type's own) | amount | |
| Asset/cash (type's own) | | amount |

### Other income approval

The mirror image of expense approval:

| Account | Debit | Credit |
|---|---|---|
| Asset/cash (type's own) | amount | |
| Income (type's own) | | amount |

### Expense budgets (FR-EXP-2)

`ExpenseBudget` only had a `status` column in the legacy schema — `ApprovedById`/`ApprovedDate`/`DeclinedById`/`DeclinedDate` were added in this phase (additive, not in the legacy schema) for consistency with Expense's/OtherIncome's approval-workflow shape. Budgets don't post GL — they're a comparison baseline for reporting, not a transaction.

### Recurring expenses (FR-EXP-1)

Same admin-triggered pattern as recurring payroll (`POST /api/expenses/run-recurring`), using `ExpenseRecurrenceRules.NextDate`. A due occurrence clones a new **Pending** `Expense` row (it still needs its own approval — recurrence doesn't bypass the workflow) dated on the due date, and advances the original's `RecurNextDate`.

## What's out of scope here

- **Fixed-asset acquisition/disposal GL posting** — only depreciation posts; buying or selling an asset doesn't write a GL entry in this phase.
- **A real scheduler** — recurring payroll and recurring expenses are both admin-triggered endpoints, not background jobs, per the established precedent (FR-SAV-4, FR-LN-25).
- **Payroll self-service** (payslips, employee portal) — this phase is the admin/finance-side run + template CRUD only.
