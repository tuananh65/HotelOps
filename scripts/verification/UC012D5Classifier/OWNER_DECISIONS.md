# D5 candidate revision3 — decisions pending, source-only

Source owner: DEV3 FE02 (PR51 API); DEV3 verification/FE02 (PR72 runner).
Security owner DEV3; runtime/SQL/HTTPS owner DEV1. This candidate is not wired
to either PR and does not change any operation's response or release gate.

## Exact policy proposed for approval

| Decision | Candidate behavior | Approval |
| --- | --- | --- |
| Installed allowed provider numbers | Empty set; no recognized503 profile | No production profile installed |
| Proposed first profile | Microsoft.Data.SqlClient.SqlException, every error number40501 | PENDING; offline test vector only |
| Multi-error collection | Every member must be allowed; duplicates allowed; empty, over64 or mixed unknown=>generic500 | PENDING exact policy |
| Duplicate2601/2627 only | Return operation-owned disposition, no HTTP override | Preserve accepted conflict handler |
| Mixed duplicate + transient | Unknown500, not automatic409/503 | PENDING exact policy |
| Deadlock1205 or timeout -2 | OutcomeUnconfirmed, no HTTP override/retry | Acceptance fails; inspect durable outcome |
| EF concurrency exception | OperationOwned, no HTTP override | Preserve operation's accepted stale/conflict semantics |
| Cancellation through supported chain | Cancelled, no HTTP override | Caller must preserve cancellation |
| Bare/unknown DbUpdateException | Generic500 INTERNAL_SERVER_ERROR | D5 revision2 direction; final signoff pending |
| Recognized storage | 503 BRANCH_STORE_UNAVAILABLE | Only after exact profile approval |

Only exact DbUpdateException wrappers are transparent, up to eight hops. Arbitrary
application wrappers, aggregate branches and subclass wrappers are not unpacked.
This is conservative handling, not a claim that unknown causes are transient.
Numbers are copied into an immutable profile; external input cannot mutate it.
A nonempty profile argument is NOT cryptographic or organizational approval.
Future integration must pin the approved profile/revision in reviewed source or
receipt; it must never accept caller/URL authority for selecting a profile.

## Fault mechanism and evidence

Current PR72 throws a bare DbUpdateException. It is suitable as an unexpected500
test only after PR51's broad catch is corrected under source-owner authorization.
It is not a recognized transient provider fault. No fake SqlException is constructed
by reflection and no synthetic collection is passed to a production caller here.

For classified503, a trial mechanism must produce the approved real provider
exception/Errors collection at the audit-write boundary in the same caller
transaction. Exact provider/engine support and reproducibility on the future trial
remain BLOCKED. In particular, proposing40501 does not prove a local SQL Server can
reproduce that Azure-associated service-busy fault. Owner must approve a feasible
provider fault profile/mechanism; do not force this proposal to fit the environment.
An interceptor throwing an arbitrary DbUpdateException is not a substitute.

Offline tests cover pure error-number classification and real EF/generic exception
chains. They cannot prove actual provider production, SQL error rollback, HTTP
mapping, audit atomicity or lease validity. Those need separately authorized trial.

Private acceptance receipt must bind profile version/approval/hash, exact candidate
assemblies, actual provider type and complete error-number collection, injection
point/count, HTTP status, Branch value/version and audit before/after rollback,
transaction outcome, no-replay evidence and exclusive lease epoch/GUID. No raw SQL,
messages, connection details, credentials or tokens. Public response remains only
the sanitized existing ProblemDetails/code. Unknown commit outcome is not PASS.

## Exact later changes, NOT made in this candidate

- PR51 be6c7278e1f5749211e97b55c8c21c0727b31fba:
  src/HotelOps.Api/Services/HotelStructure/BranchAdministrationService.cs:135–136.
  Replace blanket SQL/DbUpdateException=>503 with approved classification after
  existing business handlers. Unknown propagates to existing sanitized500 boundary.
  Keep cancellation, transaction disposal, final authority and no retry unchanged.
  A production classifier file would require separate owner-approved integration.
- PR72 d051a4a9af3b0abfdc014e9dc48ee3dbe7de2fd2:
  scripts/verification/UC012Acceptance/Host.cs:24–31, Cases.cs:92–102,
  Guards.cs TrialReceipt and Program.cs report. Add distinct approved fault profiles,
  two rollback subcases in AUDIT_ROLLBACK and versioned receipt evidence. Reject
  ambiguous old single-status receipts. Do not accept either status as equivalent.
- Preserve409 duplicate,400 validation/malformed,428 missing tag,412 stale,
  401 session,403 authority and200 no-op. Add focused regressions for these routes,
  exact exception chains, mixed errors and sanitized response fields. Real two-case
  persisted rollback and race tests remain SQL/HTTPS NOT_RUN.

No SQL/seeder/principal provisioning/HTTPS/lease/activation permission is supplied.
PR51 MERGE_HOLD, PR75 DRAFT, FE12 HOLD; no automatic merge or new runtime approval.
