# Phase 9 Spec — Communications & Reporting

| | |
|---|---|
| **Status** | SMS/email provider genuinely unconfirmed (FRD §18 marks both "[VALIDATE WITH BUSINESS]") — built as generic, pluggable adapters, not a vendor SDK. Report column sets are a documented modeling choice (no per-report spec exists in the FRD) |
| **Resolves** | BR-COM-1..5/FR-COM-1..4, BR-RPT-1..2/FR-RPT-1..2 (BR-RPT-3/FR-RPT-3's dashboard is explicitly optional pending a confirmed KPI list — not built) |
| **Implementation** | `BCKash.Domain/{Communications,Reporting}/*.cs`, `BCKash.Application/{Communications,Reporting}/*.cs`, `BCKash.Infrastructure/{Communications,Reporting}/*.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/{Communications,Reporting}/*.cs`, `tests/BCKash.Api.IntegrationTests/{Communications,Reporting}/*.cs` |

## SMS and email delivery — deliberately generic, not vendor-specific

FRD §18 explicitly leaves both the SMS gateway and the email provider as "[VALIDATE WITH BUSINESS]" — nothing is confirmed. FR-COM-3 already specifies the intended *design*, though: a pluggable, URL/template-based SMS adapter matching the legacy `sms_gateways` table, "so BCKash can point at their chosen provider without a code change." This phase builds exactly that rather than guessing a vendor:

- **SMS** (`HttpSmsGatewaySender`): a plain HTTP GET against the configured gateway's `Url`, with the recipient phone, message text, and a sender id appended as query parameters under whatever parameter *names* that gateway's `ToName`/`MsgName`/`FromName` columns specify. `SmsGateway.Name` is used as the sender-id *value* sent under the `FromName` parameter — the legacy schema has no separate "sender id value" column, so this is a documented modeling assumption. There is no per-campaign gateway selection column in the legacy schema, so a campaign send uses whichever single `SmsGateway` row is configured (most recently created, if more than one exists) — matching a business that's pointed the system at one provider.
- **Email** (`SmtpEmailSender`, via MailKit): plain SMTP against `Email:SmtpHost`/`SmtpPort`/`FromAddress` (already in appsettings, pointed at Brevo's relay) plus new `Email:SmtpUsername`/`SmtpPassword` keys, supplied via `dotnet user-secrets` — same pattern as `Jwt:SigningKey`.

Both interfaces (`ISmsSender`, `IEmailSender`) are swapped for `RecordingSmsSender`/`RecordingEmailSender` fakes whenever `Testing:UseSqlite` is set — the same config flag that already switches the DB provider for the integration-test host — since no test environment can actually reach a real SMTP relay or SMS gateway.

## Recipient targeting (BR-COM-1, FR-COM-1)

FR-COM-1 names the recipient categories and filters but doesn't define their predicates. Locked in here (`CampaignRecipientService`):

- **AllClients / ActiveClients / ProspectiveClients** — `Client.Status` is (any) / `Active` / `Pending`, respectively.
- **ActiveLoans** — clients with at least one `Disbursed` loan.
- **LoansInArrears** — clients with at least one loan carrying an unpaid, past-due repayment schedule row (*any* days late — the broader set).
- **OverdueLoans** — clients with at least one loan already flagged NPA (`Loan.IsNpa`) — the *stricter* subset past the product's configured `NpaDays` threshold. This is what distinguishes "arrears" from "overdue" in this codebase's terms; a loan can be in the arrears set without (yet) being in the overdue set.
- **HappyBirthday** — `Client.Dob`'s day-of-year falls within `[FromDay, ToDay]` (both parsed as day-of-year integers, wrapping across the new year if `FromDay > ToDay`); defaults to today's day-of-year if either is unset.
- **Office/LoanOfficer/LoanProduct/LoanStatus filters** narrow further, on top of whichever category is selected.
- The **"date range"** filter FR-COM-1 mentions has no backing column on `CommunicationCampaign` (`FromDay`/`ToDay` are specifically the birthday window) — not implemented, since inventing a new column wasn't in scope.

## Campaign and report scheduling — the established "no scheduler" pattern

No job scheduler (Hangfire, Quartz, or any recurring `IHostedService`/`BackgroundService`) exists anywhere in this codebase — this is the fourth phase to hit that same situation (after NPA recompute, savings interest, and Phase 8's payroll/expense recurrence), and it's resolved the same way each time: an admin/cron-triggered "run due items" endpoint, safe to call as often as desired, since each item only acts when its own `NextRunDate` has actually arrived.

- `POST /api/campaigns/run-due` and `POST /api/report-schedules/run-due` are the Phase 9 instances of this pattern.
- Advancing `NextRunDate` on a run is computed from the **occurrence's own due date**, not from "today" — so the next run lands on a predictable date independent of when the admin happens to trigger the endpoint (a bug caught by `CampaignSchedulingTests` during development: advancing from `today` instead of the due date produced an off-by-one-day next-run date whenever the endpoint was called a day late).

## The report catalog — one generic tabular shape, not 29 bespoke types

FRD §14 names all 29 reports verbatim (matching `ScheduledReportName`) but gives no column/figure spec for any of them. Rather than inventing 29 bespoke response types and 29 bespoke PDF/CSV/XLS export code paths, every report returns the same generic `ReportResult` (`Columns: string[]`, `Rows: string?[][]`) — see `ReportModels.cs`. This lets `IReportExporter` and `IReportSchedulerService` handle all 29 uniformly, and lets `ReportsController` expose the whole catalog through one dispatch endpoint (`GET /api/reports/{reportName}`) rather than 29 routes.

Column choices are a documented modeling decision per report (no FRD spec to match against), grouped into six category services mirroring `ScheduledReportCategory`:

- **Client** (`ClientReportService`): client numbers (counts by status per office), clients overview (a filtered listing), top clients (ranked by total disbursed).
- **Loan** (`LoanReportService`, the largest — 9 reports): disbursed loans, loan portfolio (outstanding principal by product), expected repayments, repayments (actual transactions), collection (expected vs. collected %), arrears (days-in-arrears + overdue amount, reusing `LoanNpaService`'s own days-in-arrears definition), loan sizes (banded distribution), individual indicator (per-client performance), loan officer performance (per-officer portfolio-at-risk %).
- **Financial/GL** (extends `IGlReportService`, 8 reports total): the 4 that already existed from Phase 6 (trial balance, balance sheet, P&L, cash flow) plus 4 new — **provisioning** (buckets outstanding principal into `LoanProvisioningCriteria` bands by days-in-arrears), **historical income statement** (P&L broken down by month), **journals report** (raw posted GL entries), and **accrued interest** (sums `InterestAccrual`/`FeeAccrual` entries — legitimately always empty today, since nothing in this codebase posts accrual entries yet, per `gl-posting-spec.md`'s own "what's out of scope" section; not a stub, an honest reflection of current capability).
- **Group** (`GroupReportService`): group overview, group breakdown (one row per membership), group indicator (per-group loan exposure).
- **Savings** (`SavingsReportService`): account listing, balance-by-product, transaction listing, and **fixed term maturity** — this schema has no fixed-term/maturity-dated savings concept at all (no maturity date, term length, or "fixed deposit" flag anywhere on `SavingsAccount`/`SavingsProduct`), so this one always returns an empty-but-real result.
- **Organisation** (`OrganisationReportService`): products summary (loan + savings products combined), audit report (from `audit_trail`, capped at 500 most recent rows).

## Export formats (FR-RPT-2)

- **CSV** — CsvHelper.
- **XLS** — ClosedXML, producing modern `.xlsx` (the legacy "XLS" format is not generated; every consumer of "Excel export" today expects `.xlsx`).
- **PDF** — PDFsharp, a plain unstyled tabular layout (no MigraDoc or other rich-layout library was added). PDFsharp's cross-platform build requires an `IFontResolver` before creating any font, even a standard sans-serif one — `SystemFontResolver` resolves against whichever TrueType sans-serif font the host OS already ships (macOS's bundled Arial, common Linux DejaVu Sans/Liberation Sans paths, or Windows' Arial) rather than embedding a font file in the repo. A deployment target without any of these installed would need a bundled font file instead — a known limitation, not a bug.

## What's out of scope here

- **The management dashboard** (BR-RPT-3/FR-RPT-3) — explicitly optional in the phase scope, "pending BCKash's confirmed KPI list." Not built; nothing in the FRD names the actual KPIs to show.
- **A real scheduler** — both campaign and report-schedule recurrence are admin-triggered endpoints, not background jobs, per the established precedent.
- **Report attachments on campaigns** (`CommunicationCampaign.ReportAttachment` — loan schedule/statement/savings statement/audit report/group indicator report) — the column exists and is captured on create/update, but nothing currently renders and attaches one of these to a campaign send; only `IReportSchedulerService` attaches a rendered report (via the full FRD §14 catalog, not this narrower five-value enum).
