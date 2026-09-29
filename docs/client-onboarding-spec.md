# Client Onboarding, Approval & Deletion Spec

| | |
|---|---|
| **Status** | Built |
| **Implementation** | `BCKash.Domain/Clients/{ClientOnboardingRules,ClientDeletionRules,BvnVerification,DeletionRequest}.cs`, `BCKash.Infrastructure/Clients/{ClientOnboardingService,ClientAccess,DeletionService,BcKashGatewayBvnVerificationProvider}.cs`, `ClientOnboardingController`, `DeletionRequestsController` |
| **Tests** | `tests/BCKash.Api.IntegrationTests/Clients/ClientOnboardingTests.cs` |

## Who does what

| Step | Who |
|---|---|
| Onboard clients (single or group) | Managers and marketers only, in the office portal (`UserTypeSlugs.CanCreateClients`). The plain `POST /clients` is closed to staff with a user_type. |
| Document a client (details, passport photo, documents, IDs, next of kin, guardians) | Only the staff member who onboarded them (`IClientAccess.CanDocumentAsync`). Anyone who can see the client can read it; notes are open to all. |
| Approve / decline a client | Controllers (`UserTypeSlugs.CanApproveClients`). A high-risk client can't be approved. |
| Clear a high-risk flag | Super admins (`POST /clients/{id}/mark-safe`), in the control portal. |
| Review deletion requests | Super admins, in the control portal. |

## Onboarding

1. **BVN check** — `POST /onboarding/bvn-check {bvn, fullName, phone}` looks the BVN up through the BC Kash MFB core gateway (`POST /identity/get_bvn`, verified only when `isBvnValid` is true) and stores the result as a `BvnVerification`. It compares first/last (and middle, when both have one) names and phone with what was typed. Names compare case- and space-insensitively; phones on their last 10 digits.
2. **Onboard** — `POST /onboarding/clients` (one) or `POST /onboarding/groups` (a group plus at least 3 clients). Each client refers to their verification by id, so saved details always come from the server's own lookup. When the lookup didn't match, the request must say `detailsSource`:
   - `bvn` — save the BVN record's name and phone.
   - `client` — keep what was typed. Needs `overrideReason`, and the client is flagged **high risk** until a super admin marks them safe.
3. Everything for one onboarding saves in one transaction. The first three group members get the roles leader, assistant and organizer (`GroupMemberRoles`).

A verification is single-use and expires after 24 hours. A BVN already on a (non-deleted) client can't be onboarded again.

## Groups

A group is approved automatically when its last member is approved; `POST /groups/{id}/activate` refuses while any member is unapproved. `GET /groups/{id}/summary` gives the group page's figures: cumulative approved loan amount across members, what's still owed on their running loans, and member counts.

## Deletion

- A client who has never been approved (and has no open loans or pending applications) can be deleted directly (`DELETE /clients/{id}`), by whoever onboarded them or a controller. It's a soft delete (`deleted_at`), and they're removed from their groups.
- A group with no approved member can be deleted directly (`DELETE /groups/{id}`); its clients stay.
- Anything approved needs `POST /clients/{id}/deletion-requests` or `POST /groups/{id}/deletion-requests` with a reason. A super admin approves (which deletes it) or rejects with a note (`/deletion-requests/{id}/approve|reject`).

## Audit trail

Audit entries now carry `entity_id` for changes to existing records, so `GET /clients/{id}/audit` lists one client's history. Onboarding writes an explicit "Onboarded" / "Onboarded (high risk)" entry with any BVN mismatches and the reason.

## Biometrics (AWS Rekognition)

- **Enrollment** — on the client page, Overview → Biometrics → *Start face capture*, by the staff member who onboarded the client. The browser runs AWS Face Liveness (the client follows on-screen prompts while the screen flashes colours), which checks a live person is present rather than a photo, screen or mask. It needs a liveness score of at least `Biometrics__LivenessThreshold` (80). The captured face becomes the client's **profile picture**; there's no separate photo upload. It can be recaptured until the client is approved, then it's locked.
- **Before disbursement** — every client receiving money (the loan's client, or each member with a group allocation) takes a new live capture on the loan page. It's compared with their enrolled face (`CompareFaces`) and must reach `Biometrics__FaceMatchThreshold` (**90%**). `POST /loans/{id}/disburse` refuses until everyone has passed within the last 24 hours. As elsewhere, accounts with no user_type are exempt.
- The browser never gets the account's AWS keys: `POST /biometrics/credentials` returns a 15-minute STS federation token whose only permission is `rekognition:StartFaceLivenessSession`.
- Face Liveness isn't available in London (`eu-west-2`), so liveness sessions run in Ireland (`Aws__LivenessRegion`, default `eu-west-1`); face comparison and S3 use `Aws__Region`.
- Every capture is kept (`client_biometrics`) with its liveness score, similarity, image and outcome, and is recorded in the client's audit trail.

## File storage (S3)

With `Aws__AccessKeyId`, `Aws__SecretAccessKey` and `Aws__S3Bucket` set, every upload goes to the bucket: images under `img/`, everything else under `documents/`. Client files are named after the client, e.g. `img/Ada-Obi-biometric-enrollment-20260928153000-1a2b3c4d.jpg` or `documents/Ada-Obi-utility-bill-….pdf`. The timestamp and short id keep names unique. Files saved on local disk before S3 was configured are still read from there.

## Configuration

`BvnGateway__BaseUrl` (default `https://core.bckashmfb.com/v1`), `BvnGateway__Email` and `BvnGateway__Password` — the gateway login. The provider logs in (`POST /initialisation/init`), reuses the returned `auth`/`accesscode` until the gateway refuses them, then logs in again once. `UseMockBvn` skips the gateway for testing: `isAMatch` returns exactly the submitted details, `isConflicting` returns random details that differ from them. Unset or empty verifies with the gateway; any other value stops the API at startup. Never set it in production. The integration tests use their own predictable fake (`FakeBvnVerificationProvider`): a BVN starting with 0 isn't found, one ending in 9 mismatches.
