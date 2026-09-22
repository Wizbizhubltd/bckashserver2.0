# Development Phase Plan
## BCKash MfB Portal — C# Rewrite — Build Plan for Claude Code

| | |
|---|---|
| **Companion documents** | BRD.md, FRD.md — read both before starting Phase 0 |
| **Target stack** | ASP.NET Core Web API (C#) + SPA frontend, EF Core against existing MySQL/MariaDB |
| **Status** | Draft v1.0 |
| **Date** | 2026-09-18 |

## How to use this document

This plan breaks the rewrite into 11 phases (0–10), each sized to be a self-contained unit of work you can hand to Claude Code as its own session/task. Phases are ordered by dependency — do not start a phase until its listed dependencies are complete and their acceptance criteria pass.

For **every phase**, follow this loop:
1. Re-read the relevant BRD (`BR-*`) and FRD (`FR-*`) sections referenced in the phase.
2. Design the entities/migrations for the phase's tables (additive only — see FRD §1.4).
3. Implement domain logic first, with unit tests, before wiring up API/UI.
4. Implement the API endpoints, with integration tests.
5. Implement the SPA screens for the phase.
6. Run the phase's acceptance criteria checklist before marking it done.
7. Update BRD.md/FRD.md if the phase's implementation surfaced a **[VALIDATE WITH BUSINESS]** answer or a design decision — keep the documents and the code in sync.

Each phase lists a rough relative size (S / M / L / XL) for planning purposes only — not a time estimate, since actual velocity depends on how much legacy business logic (interest calculation especially) turns out to be undocumented.

---

## Phase 0 — Foundation & Project Setup
**Size:** M
**Depends on:** nothing
**FRD sections:** §1 (Architecture), §1.4 (DB strategy), §1.5 (Authorization redesign)

**Scope:**
- Create the solution structure (§1.3 of FRD): Api, Application, Domain, Infrastructure, SharedKernel projects, plus test projects.
- Set up EF Core with the Pomelo MySQL provider; connect to a copy of the existing production database; reverse-engineer/scaffold the full 74-table schema into EF Core entities as a faithful baseline (no schema changes yet).
- Stand up the SPA project (React + TypeScript recommended) with routing, auth-aware API client, and a component library/theme.
- Implement authentication: JWT issuance, refresh tokens, TOTP 2FA support, login throttling — against the existing `users` table (read legacy password hash format first; do not break existing logins until a migration/re-hash strategy is decided per NFR-4).
- Implement the RBAC redesign proposed in FRD §1.5: `Permission` (tree), `Role`, `User`, join tables — migrate existing `roles`/`permissions`/`role_users` data into the new model, parsing whatever is in the legacy free-text `permissions` columns.
- Implement the cross-cutting audit logging interceptor (FR-SEC-6).
- Set up CI (build + test on every commit) and a code formatting/linting baseline.
- Set up centralized configuration/secrets handling (connection strings, JWT signing key, SMS/email provider credentials as placeholders).

**Acceptance criteria:**
- [ ] Solution builds; all layers reference correctly (no circular dependencies).
- [ ] EF Core model matches the production schema exactly (verified by comparing scaffolded DDL to the source `bckbbjxq_real1.sql`).
- [ ] A seeded test user can log in via the API and receive a valid JWT; 2FA flow works end-to-end for a user with `enable_google2fa = true`.
- [ ] Login throttling locks out after configured failed attempts and unlocks correctly after the cooldown.
- [ ] A permission-gated endpoint (dummy) correctly allows/denies based on role.
- [ ] Every create/update/delete on a dummy audited entity produces an `audit_trail` row with correct user/module/action.
- [ ] CI pipeline runs unit tests on push and fails the build on test failure.
- [ ] SPA can authenticate against the API and display a placeholder authenticated home page.

---

## Phase 1 — Organization & Reference Data
**Size:** S
**Depends on:** Phase 0
**BRD:** §6.1 | **FRD:** §3

**Scope:**
- CRUD for: Offices (with hierarchy), Currencies, Countries (seed once from legacy data), Funds, Payment Types (+ Payment Details), Charges, Custom Fields (+ metadata).
- Admin UI screens for each of the above.
- Settings table exposed as a simple key/value admin screen.

**Acceptance criteria:**
- [ ] Office hierarchy CRUD prevents circular parent references.
- [ ] Deactivating an office in use by active clients/loans requires explicit confirmation and is audited.
- [ ] Charges can be scoped correctly to loan/savings/shares/client and validated for their charge_option/charge_type combination.
- [ ] Custom fields of every field type (number, text, date, decimal, textarea, checkbox, radio, select) can be defined and a value captured against a test record.
- [ ] All reference data from the legacy `countries`, `currencies`, `payment_types` tables is present after migration.

---

## Phase 2 — Client Management
**Size:** M
**Depends on:** Phase 1
**BRD:** §6.3 | **FRD:** §5

**Scope:**
- Client CRUD (individual/business/ngo/other) with full KYC profile and address hierarchy.
- Client status lifecycle: pending → active → inactive → closed, declined path, all transitions audited with reason/date/actor.
- Identification records, next-of-kin, next-of-guardian, relationship types.
- Document attachment (upload/download) wired to the abstracted file storage service.
- Notes.
- Client search/filter screen (name, account number, BVN, mobile, office, status, staff).
- Auto-generated unique account numbers, preserving legacy `old_account_no` on migrated records (migration itself happens in Phase 9, but the field/behavior must exist now).

**Acceptance criteria:**
- [ ] All `FR-CLI-*` requirements implemented and unit/integration tested.
- [ ] Every client status transition is only reachable from its valid predecessor state (state machine enforced server-side, not just in UI).
- [ ] File upload/download round-trips correctly for at least PDF and image attachments.
- [ ] Client list screen supports the required filters and pagination (NFR-3).
- [ ] **[VALIDATE WITH BUSINESS]** items FR-CLI-5 and FR-CLI-6-adjacent BVN validation are either resolved with a documented business answer, or explicitly deferred with a tracked follow-up.

---

## Phase 3 — Group Lending
**Size:** S
**Depends on:** Phase 2
**BRD:** §6.4 | **FRD:** §6

**Scope:**
- Group CRUD with the same status lifecycle pattern as clients.
- Add/remove client membership, with membership history retained.
- Group screens: member roster, group profile.

**Acceptance criteria:**
- [ ] Group status lifecycle matches FR-GRP-1 and is enforced server-side.
- [ ] Membership add/remove is logged (who, when).
- [ ] Group loan allocation entities exist and are ready for Phase 4 to use, but group-loan functional behavior itself is tested in Phase 4.

---

## Phase 4 — Loan Products & Loan Origination
**Size:** L
**Depends on:** Phase 2, Phase 3
**BRD:** §6.5 (BR-LN-1 to BR-LN-3) | **FRD:** §7.1, §7.2

> **Before writing any interest/schedule code in this or the next phase**, complete FR-LN-15: extract the legacy interest/amortization algorithm from the legacy PHP codebase (not just this schema) with worked examples from real historical loans, and get it signed off. This is the highest-risk item in the whole rewrite — do not guess at the formula from the schema alone.

**Scope:**
- Loan Product CRUD (full field set per FR-LN-1/FR-LN-2), including GL account mapping fields (GL accounts themselves are stubbed/seeded here; full GL posting logic lands in Phase 6).
- Loan Purposes CRUD.
- Loan Application workflow: create, validate against product min/max (FR-LN-4), approve (creates/links `Loan` record), decline, need-changes.
- Loan header record (`Loan` entity) created in `new`/`pending` status at approval, not yet disbursed.
- Guarantor and collateral capture at the application stage (FR-LN-21, FR-LN-22).

**Acceptance criteria:**
- [ ] Loan product validation rejects min > default > max violations.
- [ ] Application → approval → `Loan` record linkage is correct and traceable (`loan_applications.loan_id`).
- [ ] Decline and need-changes paths behave per FR-LN-6/FR-LN-7.
- [ ] Guarantor and collateral records can be attached to an application/loan and are retrievable.
- [ ] Legacy interest/amortization algorithm is documented as a standalone spec (e.g., `docs/interest-calculation-spec.md`) with at least 5 worked examples matching legacy output, reviewed and approved before Phase 5 starts.

---

## Phase 5 — Loan Servicing (Disbursement, Schedule, Repayments, Reschedule, Write-off)
**Size:** XL
**Depends on:** Phase 4 (and its interest-spec sign-off)
**BRD:** §6.5 (BR-LN-4 to BR-LN-11) | **FRD:** §7.3–§7.7

**Scope:**
- Disbursement workflow (FR-LN-8 to FR-LN-11), generating the full repayment schedule using the signed-off interest/amortization spec.
- Repayment schedule domain service: flat/declining-balance × equal-installment/equal-principal, grace periods, day-count conventions — fully unit tested against the worked examples from Phase 4.
- Loan transactions: full transaction type set (FR-LN-17), repayment allocation strategy (FR-LN-16), reversal (FR-LN-18), overpayment handling (FR-LN-19), waivers (FR-LN-20).
- Loan-level charges (disbursement, installment, overdue, rescheduling fees) per FR-LN-6/FR-ORG-5.
- Reschedule request workflow (FR-LN-23).
- Write-off and write-off recovery (FR-LN-24).
- NPA flagging and income suspension (FR-LN-25).
- Loan servicing screens: loan detail/timeline, repayment entry, schedule view, reschedule/write-off actions.
- **Note:** GL posting hooks should be built as an interface (`ILoanGlPostingService`) implemented as a no-op/stub in this phase, with the real implementation landing in Phase 6 — this lets loan servicing be tested independently of GL before GL exists.

**Acceptance criteria:**
- [ ] Domain-level unit tests reproduce the exact schedule (principal/interest per installment) for all worked examples from the Phase 4 spec, for both interest methods and both amortization methods.
- [ ] A repayment correctly allocates across principal/interest/fees/penalty per each of the three configured strategies.
- [ ] Reversing a repayment restores the schedule and loan balance to their pre-transaction state exactly.
- [ ] Reschedule approval regenerates the schedule correctly from the reschedule-from date.
- [ ] Write-off moves outstanding balances out of the active portfolio and a subsequent recovery payment is correctly recorded.
- [ ] A loan whose arrears exceed `npa_days` is flagged NPA and (if configured) suspends income recognition.
- [ ] End-to-end test: create client → apply for loan → approve → disburse → make several repayments (on-time, late, partial) → confirm final schedule and balances match hand-calculated expectations.

---

## Phase 6 — General Ledger & Accounting
**Size:** L
**Depends on:** Phase 5 (loan transactions must exist to post against), Phase 1 (offices/funds)
**BRD:** §6.7 | **FRD:** §9

**Scope:**
- Chart of accounts CRUD (hierarchical, typed, `manual_entries` flag).
- Journal entry posting engine: implement `ILoanGlPostingService` (and equivalents for savings/payroll/assets in their respective phases) to post real, balanced entries for every transaction type defined in FR-GL-2.
- Manual journal entry workflow with approval (FR-GL-3).
- GL closure (FR-GL-4) and reopen-with-audit.
- Inter-office transfers (FR-GL-5).
- Journal entry reversal (FR-GL-6).
- Trial balance, balance sheet, P&L, cash flow report generation (FR-GL-7) — can be basic/unstyled at this stage; polish happens in Phase 8's reporting pass.

**Acceptance criteria:**
- [ ] Every loan transaction type from Phase 5 now produces correct, balanced journal entries traceable back to the source transaction.
- [ ] Manual entries cannot post to an account with `manual_entries = false`.
- [ ] GL closure blocks new/backdated postings on/before the closure date; reopening requires elevated permission and is audited.
- [ ] Trial balance nets to zero for a test dataset spanning multiple transaction types.
- [ ] Re-run Phase 5's end-to-end test and confirm every loan transaction in it now has matching GL entries.

---

## Phase 7 — Savings Management
**Size:** L
**Depends on:** Phase 2 (clients), Phase 6 (GL posting)
**BRD:** §6.6 | **FRD:** §8

**Scope:**
- Savings Product CRUD (FR-SAV-1).
- Savings account lifecycle (FR-SAV-2).
- Deposit/withdrawal transactions with minimum-balance and overdraft enforcement (FR-SAV-3).
- Interest accrual/posting background job (FR-SAV-4).
- Savings charges (FR-SAV-5).
- Loan↔savings transfers (FR-SAV-6).
- Savings GL posting implementation (mirrors Phase 6 pattern).
- Savings screens: account detail, transaction entry, statement view.

**Acceptance criteria:**
- [ ] Withdrawal below minimum balance is rejected unless overdraft is enabled and within limit.
- [ ] Interest accrual job produces correct interest for daily and average-balance calculation types, verified against hand-calculated test cases.
- [ ] Interest posting occurs on the configured posting period and generates correct GL entries.
- [ ] A loan repayment funded from a savings account correctly debits savings and credits the loan in a single atomic operation.

---

## Phase 8 — Payroll, Fixed Assets, Expenses & Other Income
**Size:** M
**Depends on:** Phase 6 (GL posting), Phase 0 (users, for payroll)
**BRD:** §6.8, §6.9, §6.10 | **FRD:** §10, §11, §12

**Scope:**
- Payroll templates, payroll runs, recurring payroll (FR-PAY-1 to FR-PAY-3).
- Fixed asset register and depreciation job (FR-AST-1, FR-AST-2) — confirm depreciation method with BCKash before finalizing the calculation (flagged in FRD §11).
- Expenses + budgets, other income, both with approval workflows (FR-EXP-1 to FR-EXP-3).
- GL posting for payroll, depreciation, expenses, other income.
- Admin/finance screens for each.

**Acceptance criteria:**
- [ ] A payroll run computes net pay correctly from a template with mixed fixed/percentage/tax line items.
- [ ] Recurring payroll and recurring expenses correctly generate their next occurrence per configured frequency.
- [ ] Depreciation job produces a correct annual schedule for a sample asset, matching the confirmed method.
- [ ] All four modules post correct GL entries, verified via trial balance.

---

## Phase 9 — Communications & Reporting
**Size:** M
**Depends on:** Phase 2 (clients), Phase 5 (loans), Phase 7 (savings), Phase 6 (GL)
**BRD:** §6.11, §6.12 | **FRD:** §13, §14

**Scope:**
- Communication campaign builder (FR-COM-1) with recipient targeting logic against real client/loan data.
- SMS gateway adapter (provider confirmed per FRD §18) and email delivery integration.
- Scheduled/recurring campaign execution (FR-COM-2).
- Reminders (FR-COM-4).
- Full report catalog from FRD §14 (client, loan, financial, group, savings/organisation reports), each with the standard filter set (FR-RPT-1).
- Report scheduling and email delivery in PDF/CSV/XLS (FR-RPT-2).
- Optional: management dashboard (FR-RPT-3), pending BCKash's confirmed KPI list.

**Acceptance criteria:**
- [ ] Each recipient category (arrears, overdue, birthdays, etc.) correctly selects the right client set against a test dataset.
- [ ] A scheduled campaign fires at its configured time and updates last/next run correctly.
- [ ] Every report in the FRD §14 catalog runs and returns figures reconciling with the underlying loan/savings/GL data for a test period.
- [ ] Scheduled reports deliver by email in all three supported formats.

---

## Phase 10 — Data Migration, Hardening & Cutover
**Size:** L
**Depends on:** all previous phases complete and individually UAT-passed
**BRD:** §6.13, §9 (Risks), §10 (Success criteria) | **FRD:** NFR-4, NFR-5, NFR-6

**Scope:**
- Build and test the full data migration pipeline from the legacy production database into the rebuilt schema, preserving legacy identifiers (BR-MIG-2).
- Reconciliation tooling: compare migrated balances (client counts, loan balances, savings balances, GL trial balance) against the legacy system and produce a signed-off variance report (BR-MIG-3).
- Security hardening pass: password migration/re-hash strategy execution, PII encryption at rest, dependency/vulnerability scan, penetration test or security review.
- Performance/load testing against realistic data volumes.
- Parallel-run period (legacy and new system both live, reconciled daily) — duration and cutover date to be agreed with BCKash.
- Final UAT sign-off per module by the corresponding BCKash business team (BRD §5 Stakeholders).
- Go-live runbook and rollback plan.

**Acceptance criteria:**
- [ ] Full migration dry-run completes with zero unreconciled variance on a production data copy.
- [ ] Security review findings are resolved or explicitly risk-accepted by BCKash.
- [ ] Parallel-run reconciliation shows matching results between legacy and new system for an agreed period.
- [ ] Every BRD §10 success criterion is met.
- [ ] BCKash sign-off obtained from each stakeholder group before go-live.

---

## Phase Dependency Graph

```
Phase 0 (Foundation)
   │
Phase 1 (Reference Data)
   │
Phase 2 (Clients) ──────────────┐
   │                            │
Phase 3 (Groups)                │
   │                            │
Phase 4 (Loan Products & Origination) ← interest spec sign-off gate
   │
Phase 5 (Loan Servicing) ───────┐
   │                            │
Phase 6 (General Ledger) ◄──────┘
   │
   ├── Phase 7 (Savings)
   │
   ├── Phase 8 (Payroll / Assets / Expenses)
   │
   └── Phase 9 (Communications & Reporting) ← needs Clients, Loans, Savings, GL
              │
        Phase 10 (Migration, Hardening & Cutover)
```

Phases 7, 8, and 9 can run in parallel workstreams once Phase 6 is complete, if BCKash wants to parallelize delivery across multiple Claude Code sessions/engineers — they don't depend on each other, only on Phase 6.
