# Functional Requirements Document (FRD)
## BCKash MfB Core Banking & Loan Management Portal — C# Rewrite

| | |
|---|---|
| **Document type** | Functional Requirements Document |
| **Companion document** | BRD.md (business requirements), DEVELOPMENT_PHASES.md (build plan) |
| **Target stack** | ASP.NET Core Web API (C#) + SPA frontend, EF Core against existing MySQL/MariaDB |
| **Status** | Draft v1.0 — for stakeholder and engineering review |
| **Date** | 2026-09-18 |

> Every functional requirement below is traceable to a `BR-*` item in the BRD and, where relevant, to specific tables/columns/enums in the existing schema. Requirement IDs use the pattern `FR-<module>-<seq>`. Items requiring a business decision are flagged **[VALIDATE WITH BUSINESS]**; items requiring an engineering decision during Phase 0 are flagged **[ENGINEERING DECISION]**.

---

## 1. Target Architecture

### 1.1 Overview

- **Backend**: ASP.NET Core Web API (.NET 8 LTS or later), organized as a modular monolith to start (clean separation by module — Clients, Loans, Savings, GL, Payroll, Assets, Expenses, Communications, Reporting, Identity) with the option to extract services later if BCKash's scale demands it. A full microservices split is **not** recommended for the initial rewrite — it adds distributed-systems risk without a proven scale need.
- **Frontend**: A separate SPA (React or Angular — **[ENGINEERING DECISION / VALIDATE WITH BUSINESS]**, recommend React + TypeScript for ecosystem maturity) consuming the API over REST/JSON. Server-rendered pages are not used; all UI state lives in the SPA.
- **Database**: Existing MySQL/MariaDB instance, accessed via **Entity Framework Core** with the Pomelo MySQL provider. The existing schema will be reverse-engineered into EF Core entities, then incrementally hardened (see §1.4).
- **Authentication**: Token-based (JWT) for the API, replacing the legacy Cartalyst Sentinel session/cookie model evidenced by `activations`, `persistences`, and `throttle` tables. Refresh-token rotation for "remember me" behavior. TOTP-based 2FA preserved (`enable_google2fa`, `google2fa_secret` on `users`).
- **Authorization**: Claims-based RBAC built on ASP.NET Core's policy/authorization framework, backed by the `roles`/`permissions`/`role_users` tables, redesigned as proper relational many-to-many rather than free-text `permissions` blobs **[ENGINEERING DECISION — see §1.5]**.
- **Background processing**: A scheduled-job framework (e.g., Hangfire or a hosted `BackgroundService` + Quartz.NET) for: interest accrual, savings interest posting, recurring payroll/expenses, scheduled reports, and scheduled SMS/email campaigns — replacing whatever cron/queue mechanism the legacy Laravel app used.
- **File storage**: Abstracted file storage service (local disk for dev, blob storage for production — **[VALIDATE WITH BUSINESS]** on target hosting) for client documents, identification attachments, collateral photos, asset files, payroll/expense attachments — replacing the legacy `documents.location` / various `files`/`picture`/`gallery` text columns.
- **Audit logging**: A cross-cutting audit interceptor (EF Core `SaveChanges` interceptor or MediatR pipeline behavior) that writes to a hardened `audit_trail` table on every create/update/delete of a financial or KYC-relevant entity, replacing the current manually-populated `audit_trail` table.

### 1.2 Layering (per module)

```
Presentation   → ASP.NET Core Controllers (thin; map DTOs ↔ commands/queries)
Application    → Use cases / handlers (CQRS-style: Commands mutate, Queries read),
                 validation (FluentValidation), business rules
Domain         → Entities, value objects, domain services (interest calculators,
                 GL posting rules, schedule generators) — the part most at risk
                 of silent regression, so it gets the heaviest unit-test coverage
Infrastructure → EF Core DbContext, repositories, external integrations
                 (SMS gateway, file storage, email)
```

### 1.3 Suggested solution structure

```
BCKash.Portal.sln
 ├─ src/
 │   ├─ BCKash.Portal.Api                (Web API host, controllers, auth)
 │   ├─ BCKash.Portal.Application        (use cases, DTOs, validators)
 │   ├─ BCKash.Portal.Domain             (entities, domain services, enums)
 │   ├─ BCKash.Portal.Infrastructure     (EF Core, repositories, external services)
 │   └─ BCKash.Portal.SharedKernel       (cross-cutting: audit, result types, exceptions)
 ├─ tests/
 │   ├─ BCKash.Portal.Domain.Tests       (interest/schedule/GL calculation tests)
 │   ├─ BCKash.Portal.Application.Tests
 │   └─ BCKash.Portal.Api.IntegrationTests
 └─ frontend/                             (separate SPA project)
```

### 1.4 Database strategy

- Phase 0 reverse-engineers the existing schema into EF Core entities as-is (documenting every table above), producing a baseline migration that matches production exactly — **no destructive changes yet**.
- Subsequent phases introduce **additive** migrations only: converting remaining MyISAM tables to InnoDB, adding missing foreign keys and indexes, and normalizing the free-text `permissions` columns — each such change ships with a rollback script and is validated against a production data copy before release.
- All money columns retain `decimal` precision matching the source (`decimal(65,4)` in legacy is oversized — recommend narrowing to `decimal(19,4)` for new/changed columns only, after confirming no existing value exceeds that range) **[ENGINEERING DECISION]**.

### 1.5 Authorization redesign proposal

The legacy `users.permissions` and `roles.permissions` are free-text columns (likely serialized arrays), and `permissions.parent_id` suggests a hierarchical permission tree. The rewrite will:
- Model `Permission` as a proper entity (id, parent, name, slug, description) forming a tree, matching the existing `permissions` table.
- Model `Role` ↔ `Permission` and `User` ↔ `Role` as many-to-many join tables (already close to `role_users`).
- Support **direct user-level permission overrides** in addition to role-based permissions, since the legacy `users.permissions` column suggests this capability existed and may be relied on **[VALIDATE WITH BUSINESS]**.
- Preserve time/day access windows (`time_limit`, `from_time`, `to_time`, `access_days`) as a claims-based policy evaluated per request.

---

## 2. Actors / Roles

Derived from the schema's user/role model and module usage patterns. Final role names/permission sets must be confirmed with BCKash **[VALIDATE WITH BUSINESS]**; the functional behavior below is role-agnostic and enforced by permission, not by hardcoded role name.

| Actor | Typical capabilities |
|---|---|
| **System Administrator** | Manage users, roles, permissions, offices, products, GL chart of accounts, system settings, SMS gateway. |
| **Loan Officer / Field Officer** | Onboard clients, create loan applications, record repayments, manage groups, view portfolio. |
| **Teller** | Record savings deposits/withdrawals, loan repayments, issue receipts. |
| **Credit / Branch Manager** | Approve/decline loan applications, disbursements, reschedules, write-offs. |
| **Finance / Accountant** | Manage GL, journal entries, closures, payroll, fixed assets, expenses, budgets, financial reports. |
| **Compliance / Auditor** | Read-only access to audit trail, KYC data, and compliance reports. |
| **Client** (if self-service is in scope) | View own loan/savings status and statements only **[VALIDATE WITH BUSINESS — see BR-CLI-5]**. |

---

## 3. Functional Requirements — Organization & Reference Data

- **FR-ORG-1**: CRUD for Offices with parent office selection, preventing circular hierarchies; deactivating an office with active clients/loans requires confirmation and is logged.
- **FR-ORG-2**: CRUD for Currencies (code, symbol, decimals, exchange rate, active flag) and Countries (reference data, seeded once).
- **FR-ORG-3**: CRUD for Funds.
- **FR-ORG-4**: CRUD for Payment Types (name, notes, `is_cash` flag); recording a transaction with a non-cash payment type requires payment detail capture (account number, cheque number, routing code, receipt number, bank).
- **FR-ORG-5**: CRUD for Charges, scoped to a product type (`loan`, `savings`, `shares`, `client`), with charge type, calculation option (flat/percentage/computed base), min/max amount, frequency, and linked income GL account. Charges attached to a product cannot be deleted if in use on an active account — deactivate instead. **[VALIDATE WITH BUSINESS — added in Phase 1]** which `charge_type`/`charge_option` combinations are legal per product is not constrained anywhere in the legacy schema; Phase 1 implements a best-effort matrix inferred from the enum values' own names (e.g. `savings_activation` → Savings only; the installment/principal/interest-due `charge_option` values → Loan only) — see `ChargeValidationRules` in `BCKash.Domain`. Confirm this matches the legacy PHP app's actual behavior before relying on it for production data entry.
- **FR-ORG-6**: CRUD for Custom Fields per category (client/loan/group/etc.) with field type (number, text, date, decimal, textarea, checkbox, radio, select) and required flag; custom field values are captured against the parent record via a new additive `custom_field_values` table (`custom_field_id`, `entity_type`, `entity_id`, `value`) — **[CORRECTED IN PHASE 1]** the legacy `custom_fields_meta` table has no record-reference or value column (verified directly against the DDL: `id, created_by_id, category, parent_id, custom_field_id, name, timestamps` — a tree, not a value store), so it cannot be what this requirement originally assumed. `custom_fields_meta` is still mapped as-is (its actual legacy purpose is unclear from schema alone); it is not used for value storage.

## 4. Functional Requirements — Identity & Access Control

- **FR-SEC-1**: User registration/creation requires: office assignment, unique email, name, and initial password (system-generated or admin-set, forced change on first login — **[VALIDATE WITH BUSINESS]** on policy).
- **FR-SEC-2**: Login accepts email + password, checks account not blocked, checks time/day access window, and enforces login throttling (lockout after N failed attempts within a window — mirror or improve on legacy `throttle` table behavior).
- **FR-SEC-3**: If `enable_google2fa` is true for the user, login requires a valid TOTP code as a second factor before a token is issued.
- **FR-SEC-4**: Every authenticated request is authorized against the user's effective permission set (role permissions ∪ direct overrides); unauthorized requests return 403 with no data leakage.
- **FR-SEC-5**: Password changes/resets are logged to the audit trail (event only, never the password value).
- **FR-SEC-6**: Every create/update/delete on: clients, loans, loan transactions, savings, savings transactions, GL journal entries, payroll, users, roles, and permissions writes an audit trail entry (user, office, module, action, before/after summary, timestamp).
- **FR-SEC-7**: Admins can view and filter the audit trail by user, office, module, action, and date range.

## 5. Functional Requirements — Client Management

- **FR-CLI-1**: Create client captures: client type (individual/business/ngo/other), title, first/middle/last name (or business/incorporation details for non-individual types), display name, DOB (individuals), gender, marital status, BVN, mobile/phone/email, full address hierarchy (street, ward, district, region, state, city, country), occupation/profession, office, and assigned staff. System generates a unique account number on creation.
  - **[RESOLVED — Phase 2 engineering decision]** Account numbers are system-generated on create in the format `CL` + a zero-padded 8-digit sequence (e.g. `CL00000001`), unique-constrained at the DB layer — new in Phase 2, since the legacy schema never enforced `account_no` uniqueness (see `bckbbjxq_real1.sql`'s `clients` table, which only has `PRIMARY KEY(id)`). `old_account_no` has no write path until Phase 9's migration import; Phase 2 always leaves it null on newly created clients. See `ClientAccountNumberFormat` (BCKash.Domain) and `ClientService` (BCKash.Infrastructure).
- **FR-CLI-2**: Client status transitions: `pending → active` (activation, requires activation date and activator), `active ⇄ inactive` (with reason), `→ declined` (with reason, from pending), `→ closed` (with reason, terminal). Each transition records the responsible user and date. Reactivation from inactive is supported (`reactivated_date`/`reactivated_by_id`).
  - **[RESOLVED — Phase 2 engineering decision]** `→ closed` is reachable from `pending`, `active`, or `inactive` — any status that once represented a live client relationship — but not from `declined`, which is its own terminal path for a client that was never approved. See `ClientStatusTransitionRules` (BCKash.Domain).
- **FR-CLI-3**: A client can have zero or more: identification records (type + document number + optional file attachment, active flag), next-of-kin records, next-of-guardian records (both referencing a configurable relationship type and capturing full contact/address), and free-text notes.
- **FR-CLI-4**: Documents (any file type) can be attached to a client, loan, group, savings account, identification, or repayment record, with a name, size, and notes.
  - **[RESOLVED for dev — Phase 2]** `IFileStorageService` implemented with a local-disk provider (`LocalDiskFileStorageService`, config section `FileStorage:RootPath`) behind an interface designed for a swap-in blob-storage implementation once **[VALIDATE WITH BUSINESS]** target hosting (§1.1) is confirmed; no other code depends on the storage mechanism. Phase 2 wires this up for client documents only (`api/clients/{clientId}/documents`); the underlying `Document` entity is already polymorphic and ready for loan/group/savings/repayment attachments in later phases.
- **FR-CLI-5**: Clients can be searched/filtered by name, account number, BVN, mobile number, office, status, and assigned staff.
  - **[RESOLVED — Phase 2 engineering decision]** `GET /api/clients` supports `search` (matches FirstName/MiddleName/LastName/DisplayName — not the legacy denormalized `FullName`), `accountNo`, `bvn`, `mobile`, `officeId`, `status`, `staffId`, and pagination — see the NFR-3 note below for the paging defaults.
- **FR-CLI-6**: **[VALIDATE WITH BUSINESS]** Confirm whether BVN format/checksum validation and/or live BVN verification against an external service is required, or whether it is free-text as in the legacy schema.
  - **[DEFERRED — Phase 2]** No business answer was available. BVN is implemented as free-text with no format/checksum validation and no live verification call, matching the legacy schema. Tracked as a follow-up: revisit if/when BCKash confirms a BVN validation requirement.

## 6. Functional Requirements — Group Lending

- **FR-GRP-1**: Create group captures: name, office, assigned staff, joined date, contact/address details. Group status lifecycle mirrors client status (`pending → active`, `active ⇄ inactive`, `→ declined`, `→ closed`), each transition logged.
  - **[RESOLVED — Phase 3 engineering decision]** `Group` was given `IAuditable` (it lacked it, unlike `Client`) so every create/update/transition is logged via the existing `audit_trail` interceptor with zero new code. The legacy `groups` table also had no `inactive_reason`/`inactive_date`/`inactive_by_id` columns (unlike `clients`), so three additive columns were added to carry the deactivate transition's reason/actor/date, matching `Client`'s equivalent fields. See `GroupStatusTransitionRules` (BCKash.Domain) and `GroupService` (BCKash.Infrastructure).
- **FR-GRP-2**: Add/remove clients to/from a group; a client may belong to more than one group **[VALIDATE WITH BUSINESS: confirm whether single-group membership should be enforced]**.
  - **[DEFERRED — Phase 3]** No business answer was available. Implemented per this requirement's own default text — a client may belong to more than one group, no cross-group uniqueness constraint. Tracked as a follow-up.
- **FR-GRP-3**: A loan can be created with `client_type = group`, in which case the total approved amount is allocated across selected group members (`group_loan_allocation`), and each member's allocated amount is individually trackable within the shared loan.

## 7. Functional Requirements — Loan Management

### 7.1 Loan Products
- **FR-LN-1**: Loan product CRUD captures all fields in BR-LN-1, including the GL account mapping (fund source, loan portfolio, receivable interest/fee/penalty, over-payments, suspended income, income interest/fee/penalty/recovery, written-off) used for automatic posting. A loan product cannot be deleted once a loan has been created against it — deactivate instead.
  - **[RESOLVED — Phase 4 engineering decision]** `LoanProduct` gained an additive `Active` bool column (the legacy schema had none, unlike `Charge`) to persist "deactivated." `DELETE` is blocked (409) once any `Loan` or `LoanApplication` references the product; otherwise a real delete is allowed. GL account fields remain plain unvalidated `int?` FKs in this phase — full GL posting/validation lands in Phase 6.
- **FR-LN-2**: Loan product validation: minimum ≤ default ≤ maximum for principal, term, and interest rate.
  - **[RESOLVED — Phase 4]** See `LoanProductValidationRules` (BCKash.Domain) — a null bound is treated as unconstrained on that side.

### 7.2 Loan Application & Approval
- **FR-LN-3**: Create a loan application for a client or group: select loan product, purpose, requested amount, term, term type, and proposed guarantors. Application status starts `pending`.
- **FR-LN-4**: Loan application amount, term, and (if the product allows override) interest rate must fall within the product's configured min/max unless an authorized override is granted **[VALIDATE WITH BUSINESS: which role can override, and is an approval step required for overrides]**.
  - **[DEFERRED — Phase 4]** `LoanApplication` has no interest-rate field to validate/store — only amount and term are captured and validated against the product's min/max. At approval, `Loan.InterestRate` is seeded from `LoanProduct.DefaultInterestRate` with no override support yet; the override question above remains unresolved and is carried forward.
- **FR-LN-5**: Approve a loan application: requires approver role/permission, captures approved amount (may differ from requested), approval notes, and date; creates (or links to) the corresponding `Loan` record in `new`/`pending` status, ready for disbursement setup.
- **FR-LN-6**: Decline a loan application: requires decline reason/notes; terminal state, application cannot be resurrected (a new application must be created).
- **FR-LN-7**: A loan (post-approval, pre-disbursement) supports a `need_changes` status allowing the loan officer to revise terms and resubmit, distinct from outright decline.
  - **[RESOLVED — Phase 4 engineering decision]** Implemented exactly as worded here — a `Loan`-level transition (`ILoanService.RequestChangesAsync`/`ResubmitAsync`, `LoanTransitionRules`), not an `LoanApplication`-level one (the `ApprovalStatus` enum backing applications has no NeedChanges value). `RequestChangesAsync` is reachable only from `LoanStatus.Pending` (the status a fresh approval creates), `ResubmitAsync` only from `NeedChanges`. This resolves `DEVELOPMENT_PHASES.md` Phase 4's scope bullet, which lists "approve, decline, need-changes" together loosely without this distinction.

### 7.3 Disbursement
- **FR-LN-8**: On disbursement: capture disbursement date, actual disbursed amount (may differ from approved), payment type/details, expected first repayment date. System generates the full repayment schedule at this point (or at approval, if the product configuration calls for it — **[VALIDATE WITH BUSINESS]**), based on: principal, interest rate/type, term, repayment frequency, amortization method, interest calculation period (daily/same), year-day convention (actual/360/364/365), month-day convention (actual/30/31), and any grace periods on principal/interest.
  - **[RESOLVED — Phase 5, best-effort]** `ILoanService.DisburseAsync` now generates the full schedule via `LoanScheduleGenerator` (principal, interest rate/type, term, repayment frequency, amortization method, calculation period, year/month-day conventions, and grace periods are all honored — see docs/interest-calculation-spec.md). Payment type/details capture at disbursement time is not implemented (no field for it on `Loan` beyond the existing `LoanOfficerId`; deferred). **The formula itself is a best-effort implementation of standard textbook amortization math, NOT extracted from or validated against BCKash's legacy PHP codebase or real historical loans** — see FR-LN-15 below for the full caveat, which still formally applies to every figure this generates.
- **FR-LN-9**: Disbursement posts a GL entry (transaction_type = `disbursement`) debiting the loan portfolio account and crediting the fund source account, per the loan product's `accounting_rule` (none/cash/accrual periodic/accrual upfront).
  - **[RESOLVED — Phase 6, best-effort, NOT validated against legacy]** `ILoanGlPostingService.PostDisbursementAsync` (real implementation, replacing Phase 5's `NoOpLoanGlPostingService` stub) debits the product's loan portfolio account and credits its fund source account — see `docs/gl-posting-spec.md`. The `accounting_rule` field (none/cash/accrual periodic/accrual upfront) is not read to vary the posting; every disbursement posts the same cash-basis pair regardless of the configured rule — accrual-basis posting variants weren't in scope.
- **FR-LN-10**: Disbursement-time charges (charge_type = `disbursement` or `disbursement_repayment`) are applied and posted per the charge configuration.
  - **[PARTIALLY IMPLEMENTED — Phase 5]** Loan-level charges can be attached manually (`api/loans/{loanId}/charges`, `LoanCharge`/`LoanChargeType`/`LoanChargeCalculationType`), but nothing auto-applies a product's configured charges at disbursement time, and charge amounts are entered directly rather than computed (percentage/installment-basis calculation needs the schedule, which doesn't exist yet).
- **FR-LN-11**: Loan status transitions to `disbursed`.
  - **[RESOLVED — Phase 5]** Implemented exactly as worded — see `LoanTransitionRules.CanDisburse` (Pending → Disbursed only; a loan in NeedChanges must be resubmitted to Pending first, per FR-LN-7).

### 7.4 Repayment Schedule & Interest Calculation — **implemented, best-effort (see FR-LN-15)**
- **FR-LN-12**: The schedule generator supports two interest methods:
  - **Flat**: interest computed once on original principal, spread evenly (or per amortization method) across installments.
  - **Declining balance**: interest computed per period on the outstanding principal balance.
  - Both combined with amortization method **equal installment** (constant total payment, principal/interest mix varies) or **equal principal** (constant principal portion, interest declines as balance reduces).
  - **[RESOLVED — Phase 5, best-effort]** All four combinations implemented in `LoanScheduleGenerator`; under Flat, Equal Installment and Equal Principal produce an identical schedule (a genuine property of flat interest, not a shortcut) — see docs/interest-calculation-spec.md Examples 1–2.
- **FR-LN-13**: Grace periods delay the start of principal repayment (`grace_on_principal`), interest accrual (`grace_on_interest_charged`), and/or interest payment (`grace_on_interest_payment`) by the configured number of periods.
  - **[RESOLVED — Phase 5, best-effort]** `grace_on_principal` periods are interest-only (declining balance) or excluded from the principal spread (flat); `grace_on_interest_charged` periods accrue no interest at all; `grace_on_interest_payment` periods accrue interest normally but defer it as a lump sum onto the first post-grace installment. See docs/interest-calculation-spec.md Example 5 and the grace-period unit tests.
- **FR-LN-14**: Each schedule line records, per installment: due date, principal/interest/fees/penalty due, and separately-tracked waived/written-off/paid amounts per component, plus a `paid` flag and total-due/advance-paid/late-paid summaries.
  - **[RESOLVED — Phase 5]** Every column is populated by `LoanRepaymentService`/`LoanWaiverService`/`LoanWriteOffService` as appropriate.
- **FR-LN-15**: **[ENGINEERING DECISION — high priority]** Before any new code is written, the exact legacy interest/schedule calculation algorithm must be extracted from the legacy PHP codebase (not just this schema) and encoded as a pinned specification with worked examples, then validated against a sample of real historical loans. This is the single highest-regression-risk area of the whole rewrite (BRD Risk table, row 1).
  - **[RESOLVED IN FORM, NOT IN SUBSTANCE — Phase 5]** Per explicit direction, this phase proceeded on a best-effort guessed formula rather than remaining blocked indefinitely. `docs/interest-calculation-spec.md` documents the formula (standard textbook microfinance amortization math, derived from the schema's own field names) with 5 worked examples, and the full loan servicing module (§7.4–§7.7 below) is now built and tested against it. **This is explicitly NOT validated against BCKash's legacy PHP codebase or real historical loans** — that extraction/validation step never happened, because the source was never available. Every schedule this system produces should be treated as provisional until real legacy data can be used to check it; the spec doc's header carries this warning permanently, not just during development.

### 7.5 Repayments & Transactions — **implemented, best-effort (see FR-LN-15)**
- **FR-LN-16**: Record a repayment: amount, payment type/details, date; system allocates the payment across due installments following the loan product's `loan_transaction_strategy` (penalty→fees→interest→principal, or principal→interest→penalty→fees, or interest→principal→penalty→fees), updating both the loan transaction and the affected schedule line(s) via a mapping record.
  - **[RESOLVED — Phase 5]** `LoanRepaymentAllocationEngine` pays unpaid installments oldest-due-first, applying the strategy's component order within each installment. Payment type/details linkage uses the existing `PaymentTypeId` field only (no `PaymentDetailId` capture in this phase's request).
- **FR-LN-17**: Support all legacy transaction types: repayment, repayment_disbursement, write_off, write_off_recovery, disbursement, interest/fee/penalty accrual, deposit, withdrawal, manual_entry, pay_charge, transfer_fund, interest, income, fee, disbursement_fee, installment_fee, specified_due_date_fee, overdue_maturity, overdue_installment_fee, loan_rescheduling_fee, penalty, interest_waiver, charge_waiver.
  - **[PARTIALLY IMPLEMENTED — Phase 5]** The `LoanTransactionType` enum (already fully modeled from Phase 0) is used for every transaction this phase writes: `Disbursement`, `Repayment`, `WriteOff`, `WriteOffRecovery`, `InterestWaiver`, `ChargeWaiver`. The remaining types (accruals, deposits/withdrawals against a linked savings account, manual entries, the fee-specific types) have no writer yet — nothing in this phase's scope needed them.
- **FR-LN-18**: A transaction can be marked reversible; reversal (system- or user-triggered) reverses the transaction's effect on the loan balance, schedule, and GL, and is itself logged (never a hard delete).
  - **[PARTIALLY IMPLEMENTED — Phase 5 + 6]** Repayments are reversible; reversal directly undoes that transaction's own schedule-mapping allocations (never a hard delete — `Reversed` is set, the row stays). This is correct for the common case (reversing the most recent repayment) but is not a full replay-based recomputation, so reversing an old transaction out of chronological order relative to later ones on the same installment isn't guaranteed to leave the schedule in the same state a full recompute would. GL reversal is now real (Phase 6) — `LoanRepaymentService.ReverseAsync` also reverses the transaction's GL batch via `IGlJournalEntryService.ReverseByReferenceAsync`.
- **FR-LN-19**: Overpayments are tracked separately (`overpayment`/`overpayment_derived`) and, per product configuration (`allocate_overpayments`), either held as credit or auto-applied to future installments.
  - **[PARTIALLY IMPLEMENTED — Phase 5]** Overpayment (amount paid beyond every installment's total outstanding) is detected and recorded on the transaction's `Overpayment`/`OverpaymentDerived` fields. It is **not** automatically cross-applied to a later, separate repayment transaction — `allocate_overpayments` isn't read to move money, only to exist as a product field. A running per-loan credit ledger would be needed for that and wasn't built.
- **FR-LN-20**: Waiving interest or a specific charge requires permission, reason/notes, and updates both the schedule line and (if applicable) suspended/recognized income GL postings.
  - **[RESOLVED — Phase 5 + 6, best-effort, NOT validated against legacy]** `POST /api/loans/{loanId}/waivers` waives a component (principal/interest/fees/penalty) on one schedule line, requires a reason, and records an `InterestWaiver`/`ChargeWaiver` transaction. GL posting is now real (Phase 6) — `LoanGlPostingRules.ForWaiver` — see `docs/gl-posting-spec.md`.

### 7.6 Guarantors & Collateral
- **FR-LN-21**: Add one or more guarantors to a loan application or loan: either an existing client (`is_client = true`, with relationship type) or an external guarantor (full name/contact captured directly), each with a guaranteed amount; `lock_funds` optionally locks the guarantor's own savings balance as security.
- **FR-LN-22**: Add one or more collateral items to a loan: type, description, estimated value, serial number, photo(s).
  - **[RESOLVED — Phase 4 engineering decision]** The legacy `collateral` table only has a `loan_id` FK, matching this requirement's literal "to a loan" wording — but Phase 4's own scope/acceptance criteria expect collateral (like `Guarantor`, which already has both FKs) to be capturable at the application stage too. An additive `Collateral.LoanApplicationId` column was added, mirroring `Guarantor`'s existing dual-FK shape. Collateral is captured against the `LoanApplication` in this phase; once approved, it stays traceable via `LoanApplication.LoanId`.

### 7.7 Rescheduling & Write-off — **implemented, best-effort (see FR-LN-15)**
- **FR-LN-23**: Submit a reschedule request: new principal, reschedule-from date, whether to recalculate interest, and notes. Status: pending → approved/rejected, each with responsible user and date. Approval regenerates the repayment schedule from the reschedule-from date forward and moves the loan to `rescheduled`/`pending_reschedule` as appropriate.
  - **[RESOLVED — Phase 5, best-effort]** `ILoanRescheduleService` implements the full request/approve/reject workflow (`LoanRescheduleRequest`, already modeled from Phase 0). Approval removes and regenerates every schedule line due on/after `reschedule_from_date` via `LoanScheduleGenerator`, keeping the same installment count and the loan's existing rate/method; lines before that date are untouched. `recalculate_interest` is captured but doesn't change the calculation — there's no separate "keep the old interest schedule, just push dates" mode. The loan always moves straight to `rescheduled` (the `pending_reschedule` intermediate status isn't used — nothing in this phase's scope needed a two-step reschedule-in-progress state).
- **FR-LN-24**: Write off a loan: requires permission, notes, and date; remaining principal/interest/fees/penalty are moved to written-off GL accounts per the loan product's `gl_account_loans_written_off_id`; loan status → `written_off`. Subsequent recovery payments post as `write_off_recovery` and to `gl_account_income_recovery_id`.
  - **[RESOLVED — Phase 5 + 6, best-effort, NOT validated against legacy]** `ILoanWriteOffService.WriteOffAsync` moves every unpaid installment's outstanding principal/interest/fees/penalty into the `*WrittenOff` schedule columns and a `WriteOff` transaction, and sets `Loan.Status = WrittenOff`. `RecordRecoveryAsync` records a `WriteOffRecovery` transaction, not mapped back onto schedule lines (the debt they represented is already written off). GL posting to the configured accounts is now real (Phase 6) — see `docs/gl-posting-spec.md`.
- **FR-LN-25**: NPA classification: a loan is flagged NPA when days-in-arrears exceeds the product's `npa_days`; if `npa_suspend_income` is set, further interest accrual is posted to the suspended-income account rather than recognized income until the loan cures or is written off.
  - **[PARTIALLY IMPLEMENTED — Phase 5]** `ILoanNpaService.RecomputeAsync` computes days-in-arrears from the oldest unpaid, overdue schedule line and persists `Loan.IsNpa`/`Loan.IncomeSuspended` (both new additive columns — the legacy schema has neither). Called after every repayment/reversal/write-off, and exposed as `POST /api/loans/{id}/recompute-npa` for a manual check — there is **no scheduled job** anywhere in this codebase to run this periodically, so a loan's NPA status can go stale between actions that trigger a recompute. "Posted to the suspended-income account" still doesn't happen even after Phase 6's GL work — that needs an interest-accrual GL writer, and nothing in this codebase writes `InterestAccrual` transactions yet (see FR-LN-17); `IncomeSuspended` remains just a flag. `GlAccountSuspendedIncomeId` exists on `LoanProduct` and is mapped-but-unused for the same reason.
- **FR-LN-26**: Loan loss provisioning is computed per configured `loan_provisioning_criteria` bands (min/max days in arrears → percentage), posting to the configured liability and expense GL accounts, typically run as a periodic (e.g., month-end) batch job — **[VALIDATE WITH BUSINESS]** on frequency and exact trigger.

## 8. Functional Requirements — Savings Management

- **FR-SAV-1**: Savings product CRUD per BR-SAV-1, including GL account mapping (savings reference, overdraft portfolio, savings control, interest, written-off, income interest/fee/penalty).
  - **[RESOLVED — Phase 7]** `ISavingsProductService`/`SavingsProductsController` (`api/savings-products`), mirroring `ILoanProductService`'s CRUD/in-use-blocked-delete shape. An additive `Active` column was added (the legacy schema has none), same Phase-4 rationale as `LoanProduct.Active`.
- **FR-SAV-2**: Open a savings account for a client or group against a product; status `pending → approved` (requires permission, sets opening balance) `→ closed/declined/withdrawn`.
  - **[RESOLVED — Phase 7]** `ISavingsAccountService`/`SavingsAccountsController` (`api/savings-accounts`). Account numbers follow the same "SV" + 8-digit-sequence pattern as Phase 2's client account numbers (`SavingsAccountNumberFormat`). Approval copies interest rate/minimum balance/overdraft flag/compounding/posting/year-days from the product onto the account (same copy-at-approval pattern as Phase 4's loan-application approval) and records an opening-balance deposit transaction. Closing/withdrawing an account with a non-zero balance is rejected (409) — the full balance must be withdrawn first.
- **FR-SAV-3**: Record deposit/withdrawal transactions with running balance; withdrawals are blocked below minimum balance unless overdraft is enabled and within the overdraft limit.
  - **[RESOLVED — Phase 7]** `ISavingsTransactionService`/`SavingsTransactionsController`. `SavingsBalanceRules.CanWithdraw` enforces the floor (minimum balance, or the negative overdraft limit when overdraft is enabled) for withdrawals, charge payments, and loan-repayment-from-savings transfers alike. `OverdraftLimit` is account-specific (the legacy schema has no product-level overdraft-limit column), so it's set explicitly at approval time rather than copied from the product.
- **FR-SAV-4**: Interest accrual job runs per the product's compounding period (daily/monthly/quarterly/biannual/annually) using the configured calculation type (daily balance / average balance) and year-day convention (360/365); interest is posted to the account per the posting period, updating `last/next_interest_calculation_date` and `last/next_interest_posting_date`.
  - **[RESOLVED — Phase 7]** `ISavingsInterestPostingService` implements both calculation types per `docs/savings-interest-spec.md`, directly from this requirement's own spec (unlike FR-LN-15, there's no "extract from legacy first" instruction here, so this isn't a best-effort guess — though exact rounding/period-boundary behavior still isn't checked against a legacy sample). **No job scheduler exists anywhere in this codebase** (same situation as FR-LN-25's NPA recompute) — `RunDueAsync` is exposed via `POST /api/savings-interest/run`, an admin-triggered stand-in, safe to call repeatedly since it only acts on accounts whose next calculation/posting date is actually due.
- **FR-SAV-5**: Savings-specific charges (activation, withdrawal fee, annual fee, monthly fee, specified due date) are applied per configuration and can be waived.
  - **[RESOLVED — Phase 7]** `ISavingsChargeService`/`SavingsChargesController` (`api/savings-accounts/{accountId}/charges`) — attach, waive, and pay a charge (balance-reducing, enforcing the same floor as a withdrawal). Amounts are entered directly rather than computed from a percentage/frequency rule, the same simplification Phase 4 applied to loan charges.
- **FR-SAV-6**: Support fund transfers between a client's savings account and a loan (repayment from savings, or disbursement to savings) as a linked pair of transactions.
  - **[RESOLVED — Phase 7, best-effort GL posting, NOT validated against legacy]** `ISavingsTransferService`/`SavingsTransfersController` (`api/savings-transfers`) — `RepayLoanFromSavingsAsync` (the acceptance-criterion path: debits savings and credits the loan's schedule in one atomic operation, with one balanced GL batch — no cash moves, so the debit leg is the savings control account rather than a fund-source account, see `LoanGlPostingRules.ForRepaymentFromSavings`) and `DisburseLoanToSavingsAsync` (the companion direction).

## 9. Functional Requirements — General Ledger & Accounting

- **FR-GL-1**: Chart of accounts CRUD: hierarchical (parent/child), typed (asset/liability/equity/income/expense), GL code, active flag, `manual_entries` flag controlling whether users can post directly to it.
  - **[RESOLVED — Phase 6]** `IGlAccountService`/`GlAccountsController` (`api/gl-accounts`), mirroring `IOfficeService`'s self-referencing-tree/cycle-prevention pattern exactly.
- **FR-GL-2**: Every financially-significant transaction across modules (loan, savings, payroll, asset, expense, other income) creates one or more balanced (debit = credit) journal entries, tagged with transaction type/sub-type and linked back to the source transaction (`loan_transaction_id`, `savings_transaction_id`, `payroll_transaction_id`, etc.) for full traceability.
  - **[PARTIALLY IMPLEMENTED — Phase 6, best-effort, NOT validated against legacy]** Every loan transaction type Phase 5's services write (disbursement, repayment, write-off, write-off recovery, interest/charge waivers) now posts balanced, traceable journal entries via `ILoanGlPostingService`/`LoanGlPostingRules` — see `docs/gl-posting-spec.md` for the full posting-rule table and its caveat (account *roles* come from the schema; posting *sides* are standard bookkeeping, not extracted from legacy). Savings, payroll, fixed-asset, expense, and other-income postings are out of scope — their own posting services land in their respective future phases.
- **FR-GL-3**: Manual journal entries require `manual_entries = true` on the target account, and go through an approval workflow (`approved` flag, approver, date, notes) before being considered posted for reporting purposes.
  - **[RESOLVED — Phase 6]** `IManualJournalEntryService`/`GlJournalEntriesController` (`api/gl/journal-entries`) — rejects entries targeting a `manual_entries = false` account, rejects unbalanced lines, and starts unapproved until `POST {reference}/approve`.
- **FR-GL-4**: GL closure: close an office's books as of a date; entries dated on/before a closure cannot be posted or modified without a privileged "reopen" action, which is itself audited.
  - **[RESOLVED — Phase 6]** `IGlClosureService`/`GlClosuresController` (`api/gl/closures`). Reopening is gated behind a separate `gl.closure-reopen` permission slug (elevated, distinct from ordinary `gl.manage`) and is modeled as an additive `GlClosure.ReopenedAt`/`ReopenedById` pair rather than deleting the row — the legacy schema has no "reopen" concept at all, so this preserves the full close/reopen history. Enforced via `IGlClosureGuard`, checked by every posting path (system-posted loan entries, manual entries, office transfers).
- **FR-GL-5**: Support office-to-office fund transfers as a matched pair of journal entries.
  - **[RESOLVED — Phase 6]** `IOfficeTransferService`/`OfficeTransfersController` (`api/gl/office-transfers`). The matched pair posts against a caller-chosen GL account (a clearing/cash-in-transit account picked at request time) since offices carry no GL account of their own to infer one from — see `IOfficeTransferService`'s doc comment.
- **FR-GL-6**: Journal entries can be reversed (flag `reversed`), never hard-deleted, preserving the audit trail.
  - **[RESOLVED — Phase 6]** `IGlJournalEntryService.ReverseByReferenceAsync` — reverses every line sharing a batch's `Reference` together (never a single leg alone, which would leave the batch unbalanced). Wired into `LoanRepaymentService.ReverseAsync` so reversing a repayment also reverses its GL batch, and exposed directly via `POST api/gl/journal-entries/{reference}/reverse` for manual entries/transfers.
- **FR-GL-7**: Trial balance, balance sheet, P&L, and cash flow reports are computed from posted (and, for closure purposes, non-reversed) journal entries, filterable by office and date range.
  - **[RESOLVED — Phase 6, basic/unstyled as explicitly allowed by the phase spec]** `IGlReportService`/`GlReportsController` (`api/gl/reports`) — trial balance, balance sheet, and P&L are computed from `Approved && !Reversed` entries with correct debit/credit-normal sign conventions per account type. Cash flow is an explicitly simplified view (net period-over-period movement across Asset-type accounts, grouped by month), not a full indirect/direct-method statement — polish deferred to Phase 8's reporting pass.

## 10. Functional Requirements — Payroll

- **FR-PAY-1**: Payroll template CRUD: define named line items with fixed or percentage values, tax flag, and display position.
  - **[RESOLVED — Phase 8]** `IPayrollTemplateService`/`PayrollTemplatesController` (`api/payroll-templates`, plus nested `{id}/line-items`), mirroring `ILoanProductService`'s CRUD shape. Delete is blocked if any payroll run already references the template.
- **FR-PAY-2**: Run payroll for an employee/user for a period: select template, capture payment method/type and bank details if non-cash, computed paid amount from template line items; posts to configured expense/asset GL accounts.
  - **[RESOLVED — Phase 8]** `IPayrollService`/`PayrollController` (`POST api/payroll/runs`). Gross pay is entered manually per run (confirmed with business — no stored base-salary field on `User`); `PayrollComputationRules` computes net pay from the template's line items (see `docs/phase8-payroll-assets-expenses-spec.md` for the fixed/percentage/tax computation order), snapshotted onto `PayrollMeta` rows so later template edits never change a past run's numbers. `IPayrollGlPostingService` posts the net amount to the run's own `GlAccountExpenseId`/`GlAccountAssetId` — the legacy schema has no payroll-template-level GL configuration, so these are captured per run rather than on the template.
- **FR-PAY-3**: Support recurring payroll with configurable frequency and start/end dates, auto-generating the next run.
  - **[RESOLVED — Phase 8]** `PayrollRecurrenceRules.NextDate` plus `IPayrollService.GenerateDueRecurringAsync`, exposed as `POST api/payroll/run-recurring`. **No job scheduler exists anywhere in this codebase** (same situation as FR-SAV-4/FR-LN-25) — this is an admin-triggered stand-in, safe to call repeatedly since it only acts on runs whose `RecurNextDate` is actually due.

## 11. Functional Requirements — Fixed Assets

- **FR-AST-1**: Asset register CRUD: type, office, purchase date/price, current value, useful life (years), salvage value, serial number, supporting files, status (active/inactive/sold/damaged/written off).
  - **[RESOLVED — Phase 8]** `IAssetTypeService`/`AssetTypesController` (`api/asset-types`) for the type/GL-account configuration, `IAssetService`/`AssetsController` (`api/assets`) for the register and its status lifecycle. Sold/written-off are terminal — no further status changes once reached. Delete is blocked once an asset has depreciation history.
- **FR-AST-2**: Annual depreciation job computes, per asset, beginning value, depreciation amount, rate, accumulated depreciation and ending value, and posts to the asset type's configured expense/contra-asset GL accounts.
  - **[RESOLVED — Phase 8]** Straight-line confirmed with business: `(cost − salvage value) / useful life`, depreciated evenly each year until the asset reaches its salvage value. `IAssetDepreciationService` (`POST api/assets/{id}/depreciate`, `POST api/assets/depreciation/run-due`) is idempotent per (asset, year). See `BCKash.Domain/Assets/AssetDepreciationRules.cs` and `docs/phase8-payroll-assets-expenses-spec.md`.

## 12. Functional Requirements — Expenses & Other Income

- **FR-EXP-1**: Expense CRUD by type and office, with approval workflow (pending/approved/declined, approver, date), optional recurrence (frequency, start/end/next date).
  - **[RESOLVED — Phase 8]** `IExpenseService`/`ExpensesController` (`api/expenses`), mirroring `LoanApplicationsController`'s approve/decline sub-resource pattern (`POST {id}/approve`, `POST {id}/decline`); GL posts only on approval. Recurrence follows FR-PAY-3's precedent — admin-triggered (`POST api/expenses/run-recurring`), no scheduler. `Expense` was given `IAuditable` in this phase (previously `IHasTimestamps` only) for FR-SEC-6 audit coverage on approve/decline.
- **FR-EXP-2**: Expense budget CRUD by type, office, and period, with its own approval workflow, used to compare actual vs. budgeted spend in reporting.
  - **[RESOLVED — Phase 8]** `IExpenseBudgetService`/`ExpenseBudgetsController` (`api/expense-budgets`). `ApprovedById`/`ApprovedDate`/`DeclinedById`/`DeclinedDate` are additive (the legacy schema's `expense_budgets` table only had a `status` column) — added for consistency with Expense's/OtherIncome's approval-workflow shape. Budgets don't post GL; they're a reporting baseline, not a transaction.
- **FR-EXP-3**: Other income CRUD by type and office with the same approval-workflow pattern as expenses, posting to configured income GL accounts.
  - **[RESOLVED — Phase 8]** `IOtherIncomeTypeService`/`OtherIncomeTypesController` (`api/other-income-types`) and `IOtherIncomeService`/`OtherIncomeController` (`api/other-income`), the mirror image of FR-EXP-1's shape (debits the type's asset account, credits its income account). No recurrence — not required by this requirement.

## 13. Functional Requirements — Client Communications

- **FR-COM-1**: Campaign builder: choose type (SMS/email), recipient category (all clients, active clients, prospective clients, active loans, loans in arrears, overdue loans, birthdays), optional filters (office, loan officer, product, loan status, date range), message template, and (for email) subject, recipients, and an optional attached report format (PDF/CSV/XLS) and report type (schedule/statement/audit/group indicator).
  - **[RESOLVED — Phase 9]** `ICampaignRecipientService`/`CampaignsController` (`api/campaigns`, plus `POST {id}/preview-recipients`). Every category's exact predicate — including how "in arrears" is distinguished from "overdue" (any lateness vs. past the product's NPA threshold) — is documented in `docs/phase9-communications-reporting-spec.md`. The "date range" filter has no backing column on `CommunicationCampaign` and isn't implemented. Report attachment (PDF/CSV/XLS + schedule/statement/audit/group-indicator) is captured but not yet rendered — see BR-COM-2's note.
- **FR-COM-2**: Support one-off immediate send and recurring scheduled send (frequency, interval, start date/time), tracking last/next run, number of runs, and number of recipients.
  - **[RESOLVED — Phase 9]** `ICommunicationCampaignService.RunAsync`/`GenerateDueRunsAsync`, exposed as `POST {id}/run` and `POST run-due`. **No job scheduler exists anywhere in this codebase** (same situation as FR-SAV-4/FR-LN-25/Phase 8's payroll and expense recurrence) — `run-due` is an admin-triggered stand-in, safe to call repeatedly.
- **FR-COM-3**: SMS gateway configuration is pluggable (URL/template-based, matching the legacy `sms_gateways` table) so BCKash can point at their chosen provider without a code change **[VALIDATE WITH BUSINESS: name the target SMS provider — this determines the concrete integration adapter to build]**.
  - **[RESOLVED — Phase 9]** Still no provider named — this remains genuinely open. `HttpSmsGatewaySender` implements the pluggable adapter itself (a GET request against the configured `SmsGateway.Url`, with recipient/message/sender-id under that gateway's own configured parameter names), so no code change is needed once BCKash names a provider — only a new `SmsGateway` row.
- **FR-COM-4**: User-facing reminders/notifications, marked complete when actioned.
  - **[RESOLVED — Phase 9]** `IReminderService`/`RemindersController` (`api/reminders`) — always scoped to the current user.

## 14. Functional Requirements — Reporting

The report catalog below is taken verbatim from the legacy `report_scheduler.report_name` enum and must be reproduced. Each report supports both on-demand generation and scheduling (FR-RPT-2).

**Client reports**: client numbers report, clients overview, top clients report.
**Loan reports**: disbursed loans report, loan portfolio report, expected repayments report, repayments report, collection report, arrears report, loan sizes report, individual indicator report, loan officer performance report.
**Financial (GL) reports**: balance sheet, trial balance, profit and loss, cash flow, provisioning, historical income statement, journals report, accrued interest.
**Group reports**: group report, group breakdown, group indicator report.
**Savings reports**: savings account report, savings balance report, savings transaction report, fixed term maturity report.
**Organisation reports**: products summary, audit report.

- **FR-RPT-1**: Every report is filterable at minimum by office, date range (with quick options: date picker / today / yesterday / tomorrow, matching legacy `start_date_type`/`end_date_type`), and where applicable by loan officer, loan status, and loan product.
  - **[RESOLVED — Phase 9]** `ReportFilter` (`BCKash.Application/Reporting/ReportModels.cs`) carries office/date-range/loan-officer/loan-status/loan-product; every report applies whichever of these are relevant to it. The quick date-range options (today/yesterday/tomorrow) aren't implemented as named shortcuts — callers pass explicit from/to dates instead.
  - All 29 reports are implemented and run against real data, dispatched through a single generic entry point (`IReportCatalogService`, `GET api/reports/{reportName}`) rather than 29 bespoke endpoints/response types — see `docs/phase9-communications-reporting-spec.md` for the column set chosen per report (the FRD names reports but specifies no columns) and which category service implements each one.
- **FR-RPT-2**: Reports can be scheduled (frequency: daily/weekly/monthly/yearly), emailed to configured recipients in PDF, CSV, or XLS format, tracking last/next run.
  - **[RESOLVED — Phase 9]** `IReportSchedulerService`/`ReportSchedulesController` (`api/report-schedules`), `POST {id}/run` and `POST run-due` (same admin-triggered scheduler precedent as FR-COM-2). `IReportExporter` renders any report to PDF (PDFsharp)/CSV (CsvHelper)/XLSX (ClosedXML — the legacy "XLS" format itself is not produced).
- **FR-RPT-3**: A dashboard (new capability, not strictly required to match legacy 1:1 but strongly recommended) surfaces key portfolio indicators (active loans, portfolio at risk, disbursements this period, collections this period) **[VALIDATE WITH BUSINESS — confirm desired KPIs]**.
  - **[NOT IMPLEMENTED — Phase 9]** Explicitly optional in the phase scope pending a confirmed KPI list, which was never provided. Not built.

---

## 15. Non-Functional Requirements

| # | Requirement |
|---|---|
| **NFR-1** | All monetary calculations use `decimal` (never floating point) end-to-end, matching or exceeding legacy precision. |
| **NFR-2** | All financially-significant writes occur inside a database transaction; partial writes (e.g., a repayment posted to the loan but not the GL) must be impossible. |
| **NFR-3** | API responses for list endpoints are paginated by default; no unbounded result sets. **[RESOLVED — Phase 2 engineering decision]** Default page size 20, max 100 (clamped server-side), per Phase 2's `PagedResult<T>` envelope (`BCKash.Api/Contracts/PagingContracts.cs`) — the convention future paginated list endpoints (e.g. Phase 3's group list) should reuse. |
| **NFR-4** | Passwords are hashed with a modern algorithm (BCrypt/Argon2), never reversible encryption; legacy password hashes are migrated with a forced reset or transparent re-hash on next successful login **[ENGINEERING DECISION]**. |
| **NFR-5** | All PII (BVN, identification numbers, DOB) is protected at rest (encryption or restricted-access storage) and in transit (TLS). |
| **NFR-6** | The system logs enough detail in `audit_trail` to reconstruct who changed what financial record and when, for at least the regulatory retention period **[VALIDATE WITH BUSINESS: retention period]**. |
| **NFR-7** | Domain logic (interest calculation, schedule generation, GL posting rules, depreciation) has automated unit test coverage with worked-example regression tests seeded from real legacy loans. |
| **NFR-8** | The API is documented (OpenAPI/Swagger) and versioned to allow the SPA and any future integrations to evolve independently of the backend. |
| **NFR-9** | The system supports the existing multi-currency and multi-office model without hardcoding a single currency or single branch. |
| **NFR-10** | Scheduled jobs (interest accrual, savings interest posting, recurring payroll/expenses, campaigns, reports) are idempotent and safe to retry after failure. |
| **NFR-11** | The system is horizontally scalable at the API tier (stateless, JWT-based auth) even if deployed initially as a single instance. |

---

## 16. Data Model Summary

The existing schema (74 tables) maps to these bounded contexts, each becoming an EF Core `DbContext`-partition or module boundary in the Domain layer:

| Bounded context | Key tables |
|---|---|
| Reference & Org | `offices`, `currencies`, `countries`, `funds`, `payment_types`, `payment_details`, `payment_type_details`, `charges`, `custom_fields`, `custom_fields_meta`, `custom_field_values` (new, additive — see FR-ORG-6), `settings` |
| Identity & Access | `users`, `roles`, `permissions`, `role_users`, `activations`, `persistences`, `throttle`, `audit_trail` |
| Clients | `clients`, `client_identifications`, `client_identification_types`, `client_next_of_kin`, `client_next_of_gaur`, `client_profession`, `client_relationships`, `client_users`, `documents`, `notes` |
| Groups | `groups`, `group_clients`, `group_users` |
| Loans | `loans`, `loan_applications`, `loan_products`, `loan_product_charges`, `loan_charges`, `loan_purposes`, `loan_repayment_schedules`, `loan_reschedule_requests`, `loan_transactions`, `loan_transaction_repayment_schedule_mappings`, `loan_provisioning_criteria`, `guarantors`, `collateral`, `collateral_types`, `group_loan_allocation` |
| Savings | `savings`, `savings_products`, `savings_charges`, `savings_product_charges`, `savings_transactions` |
| General Ledger | `gl_accounts`, `gl_journal_entries`, `gl_closures`, `office_transactions` |
| Payroll | `payroll`, `payroll_meta`, `payroll_templates`, `payroll_template_meta` |
| Fixed Assets | `assets`, `asset_depreciation`, `asset_types` |
| Expenses & Income | `expenses`, `expense_budgets`, `expense_types`, `other_income`, `other_income_types` |
| Communications | `communication_campaigns`, `sms_gateways`, `reminders` |
| Reporting | `report_scheduler`, `report_scheduler_run_history` |
| System | `migrations` (legacy — not needed in EF Core, which uses its own migrations table) |

A full ERD should be generated programmatically from the EF Core model in Phase 0 (e.g., via `dotnet ef dbcontext scaffold` output reviewed, plus a generated diagram) rather than hand-drawn, to guarantee it matches the real schema exactly.

---

## 17. API Design Conventions

- RESTful resource-oriented routes, e.g. `GET /api/loans/{id}`, `POST /api/loans/{id}/repayments`, `POST /api/loan-applications/{id}/approve`.
- State-transition actions (approve, decline, disburse, write-off, reschedule, activate, close) are modeled as `POST` sub-resource actions, not generic `PATCH`, so each transition can carry its own required fields and validation and its own audit event.
- All list endpoints support `page`, `pageSize`, filtering query parameters (per module, e.g., `officeId`, `status`, `dateFrom`, `dateTo`), and sorting.
- Errors follow RFC 7807 Problem Details format.
- All monetary amounts are serialized as strings or fixed-precision numbers (never floats) to avoid client-side precision loss.
- Every mutating endpoint requires an explicit permission claim, enforced by an authorization policy named after the `permissions.slug` it corresponds to — preserving a direct line from the legacy permission model to the new one.

---

## 18. Integration Points (Candidate — require confirmation)

| Integration | Evidence in schema | Status |
|---|---|---|
| SMS gateway | `sms_gateways` table, generic URL/template config | In scope — adapter pattern, provider TBD **[VALIDATE WITH BUSINESS]** |
| Email delivery | `communication_campaigns.type = 'email'`, `email_recipients`/`email_subject` | In scope — needs SMTP/provider config **[VALIDATE WITH BUSINESS]** |
| BVN verification | `clients.bvn` free-text field only, no external call evidenced | Out of scope unless BCKash requests live verification **[VALIDATE WITH BUSINESS]** |
| Core/CBN regulatory submission | Not evidenced in schema | Out of scope unless BCKash provides format/spec **[VALIDATE WITH BUSINESS]** |
| Payment gateway / card processing | Not evidenced (payment types are manually recorded) | Out of scope unless BCKash requests it **[VALIDATE WITH BUSINESS]** |

---

## 19. Traceability

Each `FR-*` requirement traces to one or more `BR-*` requirements in BRD.md §6 (same module prefix, e.g., `FR-LN-*` implements `BR-LN-*`). The development plan (`DEVELOPMENT_PHASES.md`) groups `FR-*` requirements into build phases; each phase's acceptance criteria reference the specific `FR-*` IDs it must satisfy. Maintain this chain when requirements change: BRD → FRD → phase plan → test cases.
