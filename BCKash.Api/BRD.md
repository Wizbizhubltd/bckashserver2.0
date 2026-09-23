# Business Requirements Document (BRD)
## BCKash MfB Core Banking & Loan Management Portal — C# Rewrite

| | |
|---|---|
| **Document type** | Business Requirements Document |
| **Project** | BCKash MfB Existing Portal — Rewrite in C# |
| **Prepared for** | BCKash Microfinance Bank |
| **Source system analyzed** | Existing production MySQL/MariaDB schema (74 tables) |
| **Status** | Draft v1.0 — for stakeholder review |
| **Date** | 2026-09-18 |

> **Note on scope derivation:** This BRD was reverse-engineered from BCKash's existing production database schema (`bckbbjxq_real1.sql`), not from a requirements workshop. Every module, status, and business rule listed below is inferred from actual tables, columns, and enumerations in that schema. Sections marked **[VALIDATE WITH BUSINESS]** flag assumptions that should be confirmed with BCKash stakeholders before Phase 1 begins.

---

## 1. Executive Summary

BCKash Microfinance Bank ("BCKash MfB") operates a web-based core banking portal that manages client onboarding, loans, savings, group lending, general ledger accounting, payroll, fixed assets, expense/income tracking, SMS/email communication campaigns, and management reporting. The existing system is built as a monolithic PHP/Laravel application (evidenced by Laravel-specific tables such as `migrations`, `activations`, `persistences`, `throttle`, and a Cartalyst Sentinel-style auth model) against a MySQL/MariaDB database using a mix of legacy MyISAM and InnoDB storage engines.

BCKash has engaged Claude Code to **rebuild this portal in C# (.NET)**, targeting a modern ASP.NET Core Web API backend with a decoupled single-page application (SPA) frontend, while **retaining the existing MySQL/MariaDB database** (schema to be cleaned up and formalized, not replaced) to minimize data-migration risk.

This document defines the business rationale, scope, stakeholders, and business-level requirements for the rewrite. It is paired with a Functional Requirements Document (FRD.md) that specifies detailed functional behavior, and a phased development plan (DEVELOPMENT_PHASES.md) that breaks the rebuild into work packages suitable for incremental delivery by Claude Code.

---

## 2. Business Context

BCKash MfB is a microfinance bank (MfB), a category of deposit-taking financial institution regulated in Nigeria by the Central Bank of Nigeria (CBN) **[VALIDATE WITH BUSINESS: confirm regulatory jurisdiction and applicable CBN/NDIC guidelines — this affects statutory reporting, KYC/BVN validation, and provisioning rules in Section 6.9]**. The core of BCKash's business is:

- Originating and servicing **loans** to individual clients and groups (solidarity/group lending), including collateralized and guaranteed loans.
- Accepting and managing **savings** deposits, including interest-bearing accounts.
- Maintaining a **general ledger** with double-entry accounting integrated to loan, savings, payroll, and fixed-asset transactions.
- Running internal operations: **payroll**, **fixed asset depreciation**, **expense and other-income tracking**, and **budgeting**.
- Communicating with clients via **SMS and email campaigns** (arrears reminders, birthday messages, statements).
- Producing **regulatory and management reports** (trial balance, profit & loss, portfolio-at-risk style arrears reports, audit trail, etc.).

The existing portal has grown organically over time (evidenced by legacy columns such as `old_client_id`, `old_account_no`, `old_group_id` used for migrated/legacy records), and BCKash wants a rewrite that preserves all current business capability while modernizing the technology stack, improving maintainability, and closing technical-debt-driven risks (mixed storage engines, no visible automated test coverage, tightly coupled monolith).

---

## 3. Business Objectives

| # | Objective | Rationale |
|---|---|---|
| BO-1 | Preserve 100% of existing business capability across all modules (loans, savings, GL, payroll, assets, expenses, communications, reporting) during the rewrite. | The bank cannot afford to lose operational functionality it depends on daily. |
| BO-2 | Migrate to a modern, supportable technology stack (C# / ASP.NET Core) with clear separation of concerns (API + SPA), replacing an aging, tightly-coupled PHP monolith. | Reduces long-term maintenance cost, improves developer velocity, widens the hiring pool. |
| BO-3 | Improve data integrity and auditability: replace mixed MyISAM/InnoDB usage with consistent transactional storage, add proper foreign-key constraints, and strengthen the existing audit trail. | MyISAM does not support transactions or foreign keys — a correctness and data-integrity risk in a financial system. |
| BO-4 | Introduce automated testing and CI/CD as a first-class part of delivery. | No evidence of automated tests in the current system; financial calculations (interest, GL postings) are high-risk to regress silently. |
| BO-5 | Maintain uninterrupted regulatory and management reporting throughout the transition. | Reports (trial balance, P&L, arrears, portfolio) are relied on for both regulatory and day-to-day operational decisions. |
| BO-6 | Improve security posture: modern password hashing, 2FA (already present as `google2fa` fields — must be preserved), session/token management, and role-based access control. | Financial system handling client PII (BVN, identification documents) and money movement. |
| BO-7 | Enable incremental, low-risk delivery so BCKash can validate each module in production-like conditions before the next is built. | Full rewrites of core banking systems carry high failure risk if done "big bang." |

---

## 4. Scope

### 4.1 In Scope

Per stakeholder decision, this rewrite covers the **full portal**, i.e., every module represented in the existing schema:

1. **Organization & Reference Data** — offices (branch hierarchy), currencies, countries, funds, payment types, charges, custom fields.
2. **Identity, Users & Access Control** — users, roles, permissions, two-factor authentication, login throttling, audit trail.
3. **Client Management** — individual and business clients, identification documents, next of kin, next of guardian, relationships, client-to-user linkage, notes, document attachments.
4. **Group Lending** — groups, group membership, group loan allocation, group-level status lifecycle.
5. **Loan Management** — loan products, loan purposes, loan applications, loan origination/approval/disbursement workflow, repayment schedules, loan transactions, charges, guarantors, collateral, rescheduling, write-off.
6. **Savings Management** — savings products, savings accounts, savings transactions, savings charges, interest posting.
7. **General Ledger & Accounting** — chart of accounts, journal entries, GL closures (period-end lock), inter-office transactions, loan/savings loss provisioning.
8. **Payroll** — payroll templates, payroll runs, payroll line items (deductions/allowances/tax), recurring payroll.
9. **Fixed Assets** — asset register, asset types, depreciation schedules.
10. **Expenses & Other Income** — operating expenses, expense budgets, other income, recurring expenses.
11. **Client Communications** — SMS/email campaigns, SMS gateway configuration, scheduled reminders.
12. **Reporting** — scheduled report generation and the full report catalog defined in the existing system (see FRD §9).
13. **Data Migration** — migrating existing production data from the current schema into the rebuilt system without loss.

### 4.2 Out of Scope (for this rewrite, unless BCKash decides otherwise)

- New product lines not present in the existing schema (e.g., shares/investments — note: `documents` and `payment_type_details` reference a `shares` type but no `shares` tables exist in the supplied schema, suggesting this was planned but not implemented; **[VALIDATE WITH BUSINESS]** whether shares/investment accounts should be added).
- Mobile native apps (the SPA should be responsive, but native iOS/Android apps are a separate initiative unless BCKash adds them to scope).
- Third-party integrations not evidenced in the schema (e.g., core CBN reporting portals, NIBSS, BVN verification API, payment gateway/card processing) — these are flagged as **candidate integrations** in the FRD but require explicit BCKash confirmation and credentials before being built.
- Historical data cleansing/deduplication beyond what is needed for a faithful migration (e.g., fixing bad legacy data) — treated as a separate data-quality workstream if needed.

### 4.3 Explicitly Preserved Legacy Behaviors

Because this is a **rewrite of an existing live system**, not a greenfield product, these existing behaviors must be preserved unless BCKash explicitly asks to change them:

- Loan interest methods: **flat** and **declining balance**, with **equal installment** and **equal principal** amortization.
- Loan repayment allocation order is configurable per product: `penalty_fees_interest_principal`, `principal_interest_penalty_fees`, or `interest_principal_penalty_fees`.
- Multi-level status workflows for loans, savings, groups, clients, expenses, and campaigns (see FRD for full state machines).
- Legacy record linkage fields (`old_client_id`, `old_account_no`, `old_group_id`) — needed for the data migration and possibly for reconciliation with legacy reports.

---

## 5. Stakeholders

| Role | Interest / Responsibility |
|---|---|
| **BCKash MfB Management / Executive Sponsor** | Owns business risk, approves phased go-live, signs off on BRD/FRD. |
| **Operations / Branch Staff (Loan Officers, Tellers)** | Primary day-to-day users: client onboarding, loan applications, disbursements, repayments, savings transactions. |
| **Credit / Approvals Team** | Approves loan applications, disbursements, reschedules, write-offs. |
| **Finance / Accounting Team** | Owns GL, chart of accounts, period closures, financial reporting, payroll, fixed assets, expenses. |
| **Compliance / Audit** | Relies on audit trail, KYC/identification data, and regulatory reports. |
| **IT / System Administrators** | Manages users, roles, permissions, offices, system settings, SMS gateway configuration. |
| **Clients (indirect)** | Recipients of SMS/email communications, subject of KYC data; may have portal login (`client_users` suggests a client-facing login exists) **[VALIDATE WITH BUSINESS: is there a client self-service portal today, and is it in scope?]**. |
| **Development Team (Claude Code + engineers)** | Implements the FRD against the phased plan; needs unambiguous, testable requirements. |

---

## 6. Business Requirements by Module

Each requirement is numbered `BR-<module>-<seq>` for traceability into the FRD and test plans.

### 6.1 Organization & Reference Data
- **BR-ORG-1**: The system shall support a hierarchical branch/office structure (parent-child offices) with an opening date, contact details, an assigned manager, and active/inactive status.
- **BR-ORG-2**: The system shall support multiple currencies with exchange rates, symbols, and decimal precision, and multiple countries for address/nationality data.
- **BR-ORG-3**: The system shall support configurable "funds" (sources of lending capital) that can be attached to loans and loan products for fund-level tracking.
- **BR-ORG-4**: The system shall support configurable payment types (cash, cheque, bank transfer, etc.) with associated payment details (account number, cheque number, routing code, receipt number, bank).
- **BR-ORG-5**: The system shall support configurable charges (fees/penalties) that can apply to loans, savings, shares, or clients, with flat or percentage-based calculation and configurable timing (disbursement, installment, overdue, etc.).
- **BR-ORG-6**: The system shall support administrator-defined custom fields per business object category for data not covered by standard fields.

### 6.2 Identity, Users & Access Control
- **BR-SEC-1**: The system shall support user accounts scoped to an office, with first/last name, email, phone, gender, and status (blocked/active).
- **BR-SEC-2**: The system shall support role-based access control: roles have a name, slug, and an associated permission set; users can hold one or more roles and/or direct permissions.
- **BR-SEC-3**: The system shall support two-factor authentication (Google Authenticator/TOTP) that can be enabled per user.
- **BR-SEC-4**: The system shall support time-of-day and day-of-week access restrictions per user or role (`time_limit`, `from_time`, `to_time`, `access_days`).
- **BR-SEC-5**: The system shall throttle repeated failed login attempts.
- **BR-SEC-6**: The system shall maintain a full audit trail of user actions (who, what module, what action, when, at which office).
- **BR-SEC-7**: The system shall support "remember me" persistent sessions and secure session/token invalidation.

### 6.3 Client Management
- **BR-CLI-1**: The system shall support individual, business, NGO, and other client types, with a full KYC profile (names, BVN, DOB, gender, marital status, contact details, address hierarchy — street/ward/district/region/state/country).
- **BR-CLI-2**: The system shall support a client lifecycle: pending → active → inactive → closed, and a declined path, each with responsible user, date, and reason captured.
- **BR-CLI-3**: The system shall support multiple identification documents per client (type + attachment), next-of-kin records, and next-of-guardian records, each with a configurable relationship type.
- **BR-CLI-4**: The system shall support attaching free-text notes and documents (identification, contracts, photos) to clients, loans, groups, savings accounts, and repayments.
- **BR-CLI-5**: The system shall support linking one or more portal user accounts to a client record **[VALIDATE WITH BUSINESS: purpose — staff impersonation/ownership vs. client self-service login]**.
  - **[DEFERRED — Phase 2]** No business answer was available. `ClientUser` remains modeled in the schema (from Phase 0/1's baseline scaffold) but is not exposed via any Phase 2 API/UI — no controller or service touches it. Revisit once BCKash clarifies whether this is staff impersonation/ownership tracking or genuine client self-service portal login — the two have materially different auth/security implications.
- **BR-CLI-6**: The system shall assign each client a unique, system-generated account number, while preserving legacy account numbers for migrated clients.
  - **[RESOLVED — Phase 2]** Implemented as `CL` + an 8-digit zero-padded sequence, unique-constrained at the DB layer. See FRD.md FR-CLI-1 for the full decision record.

### 6.4 Group Lending
- **BR-GRP-1**: The system shall support client groups with their own lifecycle (pending/active/inactive/declined/closed), office assignment, and an assigned staff member.
- **BR-GRP-2**: The system shall support adding/removing individual clients to/from a group and tracking historical membership.
  - **[RESOLVED — Phase 3]** The legacy `group_clients` table had no removal-tracking or actor columns at all. Three additive columns (`created_by_id`, `removed_at`, `removed_by_id`) were added — removing a member sets `removed_at`/`removed_by_id` rather than deleting the row, so the full membership timeline (including past memberships) stays queryable; re-adding a removed client creates a new row rather than reviving the old one. See `GroupClient` (BCKash.Domain) and `IGroupMembershipService`/`GroupMembershipService`.
- **BR-GRP-3**: The system shall support loans issued at the group level with the loan amount allocated across individual group members (joint liability / solidarity lending).

### 6.5 Loan Management
- **BR-LN-1**: The system shall support configurable loan products defining: principal range and default, term range and default (in days/weeks/months/years), repayment frequency, interest rate range and default, interest type (flat/declining balance), amortization method (equal installment/equal principal), interest calculation period, grace periods (on principal, on interest charged, on interest payment), an interest calculation basis (actual/360/364/365 days per year), a repayment allocation strategy, and linked GL accounts for automated posting.
- **BR-LN-2**: The system shall support a loan application workflow, separate from the disbursed loan record, that captures the requested amount, term, purpose, and proposed guarantors, and results in an approved/pending/declined outcome.
- **BR-LN-3**: The system shall support the full loan lifecycle: new → pending → approved → (need changes) → disbursed → (rescheduled / written off) → closed / paid, with withdrawal and decline as terminal alternate paths — capturing the responsible user, date, and notes at every transition.
  - **[RESOLVED — Phase 5, best-effort]** `pending → disbursed → rescheduled` and `disbursed → written_off` are now built, alongside Phase 4's `pending ⇄ need_changes`. `closed`/`paid`/`withdrawn` are not — nothing in Phase 5's acceptance criteria required a loan-closing action, and there's no "mark closed once fully repaid" trigger built. See BR-LN-4's note for the shared caveat every figure below carries.
- **BR-LN-4**: The system shall generate a repayment schedule per loan (principal, interest, fees, and penalty due per installment) based on the product's amortization and interest method, and shall track waived, written-off, and paid amounts per component per installment.
  - **[RESOLVED — Phase 5, best-effort, NOT validated against legacy]** Implemented per explicit direction to proceed on a best-effort guessed formula rather than remain blocked — see `docs/interest-calculation-spec.md` for the full formula and worked examples, and FRD.md's FR-LN-15 for the unchanged substance of the caveat: no legacy PHP source was ever available, so nothing here has been checked against BCKash's actual historical loans. Every other BR-LN-* item below that depends on the schedule (BR-LN-5, BR-LN-9, BR-LN-10, BR-LN-11) inherits this same caveat.
- **BR-LN-5**: The system shall record all loan-affecting transactions (disbursement, repayment, fees, penalties, interest accrual, write-off, write-off recovery, interest/charge waivers, reschedule fee, manual entries) with the ability to reverse a transaction (system- or user-initiated) and to map a transaction's allocation across schedule line items (principal/interest/fees/penalty/overpayment).
  - **[PARTIALLY IMPLEMENTED — Phase 5, GL posting added Phase 6]** Disbursement, repayment, write-off, write-off recovery, and interest/charge waivers are recorded, with repayments reversible and mapped onto schedule lines via `LoanTransactionRepaymentScheduleMapping`. Accrual, reschedule-fee, and manual-entry transaction types aren't written by anything yet. See FRD.md FR-LN-16 to FR-LN-19 for the itemized detail (allocation strategy, reversal's out-of-order limitation, overpayment not auto-applying to future transactions). Each of these transaction types now also posts a real, balanced GL entry batch (Phase 6) — see `docs/gl-posting-spec.md`; reversing a repayment now reverses its GL batch too.
- **BR-LN-6**: The system shall support loan-specific charges (disbursement fees, installment fees, overdue fees, rescheduling fees) that can be flat or calculated from principal/interest/total due, with support for waiving individual charges.
  - **[PARTIALLY IMPLEMENTED — Phase 4 + 5]** Charges can be manually attached to a loan (`api/loans/{loanId}/charges`, Phase 4) with a directly-entered amount, and a schedule-line component can now be waived (`api/loans/{loanId}/waivers`, Phase 5, FR-LN-20). Automatic percentage/installment-basis charge calculation still isn't built.
- **BR-LN-7**: The system shall support one or more guarantors per loan (client-guarantors or external guarantors), capturing the guaranteed amount and optionally locking guarantor funds.
- **BR-LN-8**: The system shall support recording collateral against a loan (type, description, value, serial number, photos).
- **BR-LN-9**: The system shall support loan rescheduling requests (new terms, reschedule-from date, optional interest recalculation) with an approve/reject workflow.
  - **[RESOLVED — Phase 5, best-effort]** `ILoanRescheduleService` — see FRD.md FR-LN-23 for exactly how approval regenerates the schedule.
- **BR-LN-10**: The system shall support configurable non-performing-asset (NPA) thresholds, arrears grace periods, income suspension on NPA loans, and provisioning criteria (percentage of portfolio by days-in-arrears band, posted to configured GL accounts).
  - **[PARTIALLY IMPLEMENTED — Phase 5]** NPA thresholds and income-suspension flagging are built (`ILoanNpaService`, on-demand recompute — see FRD.md FR-LN-25). Provisioning (`LoanProvisioningCriteria`, GL posting) is not — that's tied to Phase 6's GL work.
- **BR-LN-11**: The system shall support loan write-off and write-off recovery, each posting to the appropriate GL accounts.
  - **[RESOLVED — Phase 5 + 6, best-effort, NOT validated against legacy]** Write-off and recovery are recorded (`ILoanWriteOffService`) and now post real, balanced GL entries (Phase 6) — see `docs/gl-posting-spec.md`.

### 6.6 Savings Management
- **BR-SAV-1**: The system shall support configurable savings products defining minimum balance, interest rate, interest compounding period, interest posting period, interest calculation type (daily/average balance), overdraft eligibility and limit, and linked GL accounts.
  - **[RESOLVED — Phase 7]** See FRD.md FR-SAV-1. The overdraft *limit* itself is account-specific, not product-level (the legacy schema has no such column on `savings_products`) — the product only carries the overdraft eligibility flag.
- **BR-SAV-2**: The system shall support a savings account lifecycle (pending → approved → closed/declined/withdrawn) at the individual or group level.
  - **[RESOLVED — Phase 7]** See FRD.md FR-SAV-2. Group-level savings is modeled (`SavingsClientType.Group`/`GroupId`) but not deeply validated against group status — the same scope-matched treatment Phase 4/5 gave group loans.
- **BR-SAV-3**: The system shall record all savings transactions (deposit, withdrawal, bank fees, interest, dividend, guarantee lock/restore, fee payment, transfers between loan and savings) with running balance and approval status.
  - **[PARTIALLY IMPLEMENTED — Phase 7]** Deposit, withdrawal, interest, fees payment, and loan/savings transfers are recorded. Bank fees, dividend, and guarantee lock/restore transaction types are modeled in the schema but have no writer yet — nothing in this phase's scope needed them.
- **BR-SAV-4**: The system shall calculate and post interest on savings accounts per the product's compounding/posting schedule, tracking last/next calculation and posting dates.
  - **[RESOLVED — Phase 7]** See FRD.md FR-SAV-4 and `docs/savings-interest-spec.md`.
- **BR-SAV-5**: The system shall support savings-specific charges (activation, withdrawal, annual, monthly, specified due date fees).
  - **[RESOLVED — Phase 7]** See FRD.md FR-SAV-5.

### 6.7 General Ledger & Accounting
- **BR-GL-1**: The system shall maintain a hierarchical chart of accounts (asset/liability/equity/income/expense) with GL codes, and flag whether manual journal entries are permitted per account.
  - **[RESOLVED — Phase 6]** See FRD.md FR-GL-1.
- **BR-GL-2**: The system shall automatically post journal entries for loan disbursements, repayments, fees, penalties, interest accrual, write-offs, recoveries, savings deposits/withdrawals/interest, payroll, and fixed-asset transactions, tagged by transaction type/sub-type and traceable back to the originating transaction.
  - **[PARTIALLY IMPLEMENTED — Phase 6 (loans) + Phase 7 (savings), best-effort, NOT validated against legacy]** Loan transactions (disbursement, repayment, write-off, write-off recovery, waivers) and savings transactions (deposit, withdrawal, interest, charges, loan↔savings transfers) both post real, balanced journal entries — see `docs/gl-posting-spec.md`, `docs/savings-interest-spec.md`, and FRD.md FR-GL-2/FR-SAV-4/FR-SAV-6. Payroll/fixed-asset posting is out of scope until their own phases.
- **BR-GL-3**: The system shall support manual journal entries with an approval workflow, distinct from system-generated entries.
  - **[RESOLVED — Phase 6]** See FRD.md FR-GL-3.
- **BR-GL-4**: The system shall support period-end GL closures per office, preventing further posting before the closure date without authorization.
  - **[RESOLVED — Phase 6]** See FRD.md FR-GL-4.
- **BR-GL-5**: The system shall support inter-office fund transfers.
  - **[RESOLVED — Phase 6]** See FRD.md FR-GL-5.
- **BR-GL-6**: The system shall support reversing journal entries.
  - **[RESOLVED — Phase 6]** See FRD.md FR-GL-6.

### 6.8 Payroll
- **BR-PAY-1**: The system shall support configurable payroll templates defining line items (allowances, deductions, tax) with fixed or percentage values.
  - **[RESOLVED — Phase 8]** See FRD.md FR-PAY-1.
- **BR-PAY-2**: The system shall support running payroll per employee/user per period, applying a template, computing net pay, and posting the corresponding expense/asset GL entries.
  - **[RESOLVED — Phase 8]** See FRD.md FR-PAY-2 and `docs/phase8-payroll-assets-expenses-spec.md`. Gross pay is entered manually per run, confirmed with business — the legacy schema has no stored base-salary field on `User`.
- **BR-PAY-3**: The system shall support recurring payroll (configurable frequency, start/end dates).
  - **[RESOLVED — Phase 8]** See FRD.md FR-PAY-3. Admin-triggered (`POST /api/payroll/run-recurring`) — no scheduler exists in this codebase, same precedent as FR-SAV-4/FR-LN-25.

### 6.9 Fixed Assets
- **BR-AST-1**: The system shall maintain an asset register (type, office, purchase date/price, value, useful life, salvage value, serial number, supporting files) with a status lifecycle (active/inactive/sold/damaged/written off).
  - **[RESOLVED — Phase 8]** See FRD.md FR-AST-1.
- **BR-AST-2**: The system shall compute and record annual depreciation per asset (beginning value, depreciation amount, rate, accumulated depreciation, ending value) posted to configured GL accounts.
  - **[RESOLVED — Phase 8]** See FRD.md FR-AST-2 and `docs/phase8-payroll-assets-expenses-spec.md`. Depreciation method confirmed straight-line with business (the flagged uncertainty is resolved).

### 6.10 Expenses & Other Income
- **BR-EXP-1**: The system shall support recording operating expenses by type and office, with an approval workflow (pending/approved/declined) and optional recurrence.
  - **[RESOLVED — Phase 8]** See FRD.md FR-EXP-1.
- **BR-EXP-2**: The system shall support expense budgeting by type, office, and period.
  - **[RESOLVED — Phase 8]** See FRD.md FR-EXP-2. `ExpenseBudget`'s approver/decliner actor+date fields are additive (Phase 8) — the legacy schema only had a `status` column.
- **BR-EXP-3**: The system shall support recording other (non-lending) income by type and office, with an approval workflow.
  - **[RESOLVED — Phase 8]** See FRD.md FR-EXP-3.

### 6.11 Client Communications
- **BR-COM-1**: The system shall support SMS and email campaigns targeting configurable recipient categories (all clients, active clients, clients with loans in arrears/overdue, birthdays this period, etc.), filterable by office, loan officer, product, and loan status.
  - **[RESOLVED — Phase 9]** See FRD.md FR-COM-1 and `docs/phase9-communications-reporting-spec.md` for the exact predicate locked in per category (FR-COM-1 names the categories but doesn't define them).
- **BR-COM-2**: The system shall support attaching a generated report (loan schedule, loan statement, savings statement, audit report, group indicator report) to a communication.
  - **[PARTIALLY IMPLEMENTED — Phase 9]** `CommunicationCampaign.ReportAttachment` exists and is captured on create/update, but nothing renders/attaches one yet — see the spec doc's "what's out of scope" section.
- **BR-COM-3**: The system shall support one-off and recurring (scheduled) campaigns, tracking last/next run and delivery counts.
  - **[RESOLVED — Phase 9]** See FRD.md FR-COM-2. Admin-triggered (`POST /api/campaigns/run-due`) — no scheduler exists in this codebase, same precedent as FR-SAV-4/FR-LN-25/Phase 8.
- **BR-COM-4**: The system shall support configurable SMS gateway integration.
  - **[RESOLVED — Phase 9]** See FRD.md FR-COM-3. FRD §18 does not actually confirm a provider — built as a generic URL/template adapter per FR-COM-3's own design intent, not a vendor SDK.
- **BR-COM-5**: The system shall support user-level reminders/notifications.
  - **[RESOLVED — Phase 9]** See FRD.md FR-COM-4.

### 6.12 Reporting
- **BR-RPT-1**: The system shall provide the full report catalog identified in the existing system (see FRD §9 for the complete list), covering client, loan, financial (GL), group, and savings/organisation report categories.
  - **[RESOLVED — Phase 9]** The FRD §9 cross-reference above is stale — the catalog is FRD §14. All 29 reports run against real data; see `docs/phase9-communications-reporting-spec.md` for the column set chosen per report (no per-report spec exists in the FRD/BRD).
- **BR-RPT-2**: The system shall support scheduling reports to run on a recurrence pattern and email the output (PDF/CSV/XLS) to configured recipients.
  - **[RESOLVED — Phase 9]** See FRD.md FR-RPT-2. Admin-triggered (`POST /api/report-schedules/run-due`), same scheduler precedent as BR-COM-3.
- **BR-RPT-3**: Financial reports (trial balance, balance sheet, P&L, cash flow) shall respect GL closure boundaries and reflect only approved/posted entries unless explicitly run in a "draft/unapproved" mode.
  - **[RESOLVED — Phase 6, carried forward unchanged in Phase 9]** All eight financial reports (the original four plus Phase 9's provisioning/historical income statement/journals/accrued interest) compute from `Approved == true && Reversed == false` entries only — see FR-GL-7. No separate "draft/unapproved" mode was added; GL closure boundaries are enforced at posting time (`IGlClosureGuard`), not re-checked at report time, since a closed period cannot receive new postings in the first place.

### 6.13 Data Migration
- **BR-MIG-1**: All active and historical client, loan, savings, group, GL, payroll, asset, expense, and communication data in the existing production database shall be migrated to the rebuilt system with no loss of financial balances or audit history.
- **BR-MIG-2**: Legacy identifiers (`old_client_id`, `old_account_no`, `old_group_id`) shall be preserved to support reconciliation against historical legacy reports.
- **BR-MIG-3**: Migrated financial totals (loan balances, savings balances, GL trial balance) shall be reconciled and signed off against the legacy system before cutover.

---

## 7. Assumptions

1. BCKash will provide read access to a representative copy (or full copy) of production data for migration testing, with PII appropriately protected/masked in non-production environments.
2. The existing MySQL/MariaDB database can be upgraded/normalized (converting MyISAM tables to InnoDB, adding foreign keys) without requiring a different database engine, per the stakeholder decision to keep MySQL/MariaDB.
3. BCKash has, or will provide, definitive answers to all **[VALIDATE WITH BUSINESS]** items before the relevant development phase begins.
4. Regulatory reporting formats (if any external CBN/NDIC submission format exists) will be supplied by BCKash; this BRD only covers reports evidenced in the existing schema.
5. The rewrite will run alongside the legacy system during development and cut over module-by-module or in a single coordinated cutover — to be decided with BCKash before Phase 9 (Data Migration & Cutover).

## 8. Constraints

1. The rebuilt system must interoperate with (or fully replace) the existing MySQL/MariaDB data without a database engine change.
2. Financial calculations (interest, GL postings, depreciation) must match the legacy system's output during the parallel-run/validation period to build stakeholder trust, even where the legacy calculation may not be textbook-perfect — deviations must be a deliberate, documented decision, not an accidental regression.
3. The system handles sensitive financial and personal data (BVN, identification documents) and must comply with applicable Nigerian data protection requirements (NDPA) **[VALIDATE WITH BUSINESS]**.

## 9. Risks

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Silent regression in interest/GL calculation logic during rewrite | High — financial misstatement | Medium | Automated unit/regression tests comparing new engine output to legacy output on historical loans; parallel run before cutover. |
| Data loss or corruption during migration | High | Medium | Reconciliation checklists (BR-MIG-3), staged migration with rollback plan, full backups before cutover. |
| Scope creep from "full portal" breadth | Medium — schedule slip | High | Strict phase gating (see DEVELOPMENT_PHASES.md); each phase has its own acceptance criteria and sign-off. |
| Undocumented business rules embedded only in legacy PHP code, not visible from the schema | Medium | High | Dedicated code-reading/discovery task at the start of each phase (see FRD assumptions); business SME review of each module's functional spec before build. |
| Ambiguous authorization model (permissions stored as free text/JSON in `users.permissions` and `roles.permissions`) | Medium | Medium | FRD Phase 1 includes explicit RBAC redesign proposal for BCKash sign-off. |
| Regulatory/compliance requirements not captured because they live outside the schema | High | Medium | Explicit **[VALIDATE WITH BUSINESS]** flags throughout; compliance stakeholder review before Phase 3 (Loans) and Phase 6 (GL). |

## 10. Success Criteria

- All in-scope modules pass user acceptance testing (UAT) by the corresponding BCKash business team (Ops, Credit, Finance, Compliance).
- Migrated financial data reconciles to the legacy system within an agreed tolerance (ideally zero variance) at cutover.
- Zero regressions in interest calculation, repayment allocation, or GL posting versus a sample of historical legacy loans/transactions, verified by automated regression tests.
- All reports in the existing report catalog are reproducible in the new system with matching figures for a common test period.
- The system passes a security review (authentication, authorization, audit trail, data protection) before go-live.

## 11. Glossary

| Term | Meaning |
|---|---|
| **MfB** | Microfinance Bank |
| **GL** | General Ledger |
| **NPA** | Non-Performing Asset (loan classification) |
| **BVN** | Bank Verification Number (Nigerian identity/KYC identifier) |
| **KYC** | Know Your Customer |
| **Declining balance** | Interest method where interest is charged on the reducing outstanding principal |
| **Flat interest** | Interest method where interest is charged on the original principal for the full term |
| **Amortization — equal installment** | Repayment schedule where each installment (principal + interest) is roughly equal |
| **Amortization — equal principal** | Repayment schedule where principal is equal each installment and interest declines |
| **Provisioning** | Setting aside GL reserves against expected loan losses, based on arrears aging |
| **PAR** | Portfolio at Risk (implied by arrears/overdue reporting) |

---

*This BRD is paired with `FRD.md` (detailed functional and non-functional requirements) and `DEVELOPMENT_PHASES.md` (phased implementation plan for Claude Code). All three documents should be kept in sync as requirements are validated with BCKash stakeholders.*
