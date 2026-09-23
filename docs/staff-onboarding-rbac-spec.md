# Staff Onboarding & RBAC Spec — user_type / user_class

| | |
|---|---|
| **Status** | Built on the existing legacy `roles`/`role_users`/`permissions` tables — no RBAC schema change. `UserClass` and the onboarding-approval fields are additive columns on `users` (not in the legacy schema) |
| **Scope** | The maker-checker rule applies to the new staff/office-onboarding flow only (this pass) — existing approval workflows (loan applications, expenses, GL journal entries, savings accounts, campaigns) are unchanged, per an explicit scoping decision |
| **Implementation** | `BCKash.Domain/Identity/{UserClass,UserOnboardingStatus,UserTypeSlugs,UserOnboardingRules}.cs`, `BCKash.Application/Identity/IUserService.cs`, `BCKash.Infrastructure/Identity/*.cs` |
| **Tests** | `tests/BCKash.Domain.Tests/Identity/UserOnboardingRulesTests.cs`, `tests/BCKash.Api.IntegrationTests/Identity/StaffOnboardingRbacTests.cs` |

## The core design decision: user_type is a Role, not a new column

"user_type" (super_admin/controller/director/manager/marketer) is **not** a new database column — it's the one `Role` row assigned to a user via the pre-existing `role_users` table whose `slug` is one of the five in `UserTypeSlugs.All`. This means:

- RBAC ("the operations a user_type can carry out") continues to flow through the exact same `Permission → RolePermission → RoleUser` chain every other phase already uses — no new authorization mechanism, no new tables, no data-migration risk.
- A staff member's user_type can be changed at any time by replacing their `RoleUser` row (`POST /api/users/{id}/change-user-type`) without touching a `user_type` column anywhere, because there isn't one.
- Adjusting what a user_type can do is an ordinary `RolePermission` data change (or via the existing role/permission admin surface), not a code change.

## user_class — a new, orthogonal restriction

`UserClass` (`Initiator` / `Authorizer` / `Reviewer`) is genuinely new — it doesn't exist in the legacy schema and has no natural home in the Role/Permission model, since it's not "a set of operations a user can do" but "which step of a two-step operation a user can perform." It's a plain additive column on `users`.

The rule, exactly as specified:
- A user_class `Initiator` can start (initiate) an operation.
- Only a user_class `Authorizer` of the **same user_type** as the initiator can approve/decline it.
- A user_class `Reviewer` can do neither — a read-only class, reserved for future use beyond "can view."
- A `super_admin` user_type bypasses this entirely — they create/approve directly regardless of their own `UserClass`.

This is enforced in `UserOnboardingRules` (pure, unit-tested) and consumed by `UserService`, not as a declarative ASP.NET authorization policy — it needs per-record context (comparing the *acting* user's type/class against the *record's initiator's* type), which a generic `[Authorize(Policy = ...)]` attribute can't express. The HTTP-level permission gate (`users.manage`) and the business-rule gate (UserClass/user_type match) are deliberately separate checks, mirroring how every other approval workflow in this codebase (e.g. `LoanApplicationsController`) splits "are you even allowed near this module" from "is this specific transition valid."

## Staff onboarding flow

1. `POST /api/users` — an Initiator (or a super admin) creates a new staff record with a system-generated temporary password (`TemporaryPasswordGenerator`), emailed to the new hire via the existing `IEmailSender` (Phase 9's SMTP infrastructure, reused as-is). A super admin's creation is auto-approved (`OnboardingStatus = Approved`) and the new hire can log in immediately; anyone else's creation leaves it `Pending`.
2. `POST /api/users/{id}/approve-onboarding` / `.../decline-onboarding` — an Authorizer of the same user_type as the initiator (or a super admin) resolves it. A decline also sets `Blocked = true` — a declined record must never be able to log in.
3. `AuthService.LoginAsync` rejects login for any `OnboardingStatus != Approved` (`LoginOutcomeType.PendingOnboarding`) — a real gap caught during development: without this check, a Pending staff record could log in immediately using the password from their own onboarding email, defeating the entire maker-checker rule.
4. `POST /api/users/{id}/assign-office` — a super admin (or anyone with `users.manage`) assigns a staff member to an office/branch. Office creation itself is untouched, existing Phase 3/4 CRUD (`organization.manage`).
5. `GET /api/users/me` — lets any authenticated user (including a control-portal frontend) discover their own user_type/user_class/onboarding status after login, without needing it baked into the JWT.

## Bootstrap: seeding the first super admin

`IdentityBootstrapSeeder` (an `IHostedService`, same pattern as `ReferenceDataSeeder`) does three things on every boot, all idempotent:

1. Seeds a `Permission` row for every slug any controller currently checks for via `[Authorize(Policy = "Permission:...")]` — closing a gap that predated this pass (no `Permission` row ever existed for any slug, so no role could actually be granted one).
2. Seeds the five user_type `Role` rows, each wired to a **default** permission set (see `IdentityBootstrapSeeder.DefaultRolePermissions`) — a reasonable starting point, not a business-confirmed mapping. `super_admin` gets every known slug; `controller`/`director` get oversight/approval-heavy slugs; `manager`/`marketer` get day-to-day operational slugs. Adjust via ordinary `RolePermission` rows at any time — no code change needed. A role that's had its permissions customized via the API is never overwritten on restart (only seeded if it currently has zero `RolePermission` rows).
3. **Only if `Bootstrap:SuperAdminEmail` is configured** (opt-in — a fresh dev/test database doesn't silently gain an admin nobody asked for) and no `super_admin` exists yet: creates the first super admin, emails them a temporary password, and self-approves (`OnboardingApprovedById = their own id` — there's no one else yet). This finally closes the "how do I get my first login" gap that previously required a hand-written SQL script — set `dotnet user-secrets set Bootstrap:SuperAdminEmail "you@yourcompany.com"` and the very first `dotnet run` creates a working, emailed super admin account.

## What's deliberately out of scope

- **Retrofitting existing approval workflows** with the user_type/user_class rule (loan applications, expenses, GL journal entries, savings accounts, campaigns) — an explicit scoping decision; they keep their current single-permission-slug model. A future pass could layer `UserClass` onto them the same way.
- **A distinct "control portal" API surface** — the control portal is a separate frontend hitting this same API with the same login/JWT/permission system; no portal-specific route prefix, audience, or CORS origin was added.
- **JWT claims for user_type/user_class** — deliberately not embedded in the access token (which would require re-issuing tokens on every type/class change to stay accurate); `GET /api/users/me` is the source of truth instead.
