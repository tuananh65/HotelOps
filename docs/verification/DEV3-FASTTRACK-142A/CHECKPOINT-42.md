# FASTTRACK-142A checkpoint42 — UC036 submission verified

Newly COMPLETE_VERIFIED: **UC036 Complete Cleaning Rework**, under the documented GBR-044 precedence decision42. Total: **6 COMPLETE_VERIFIED (UC035, UC036, UC044, UC046, UC109, UC110), 138 PARTIAL**. The original deadline remains 2026-11-16 17:50 UTC. This checkpoint continues the sprint; it is not a release or production acceptance.

## Direct acceptance evidence

Final runtime receipt: `checkpoint42-submission-committed-e2e.json`, success=true, normal and effectively blocked rooms, with cleanup. These are new executions, not inherited handoff claims.

| UC036 acceptance | Direct verification |
| --- | --- |
| Authorized current executor, own In Progress rework | Existing real login/receive/Accept/Start precedes Submit; backend retains current session, CLEANING_EXECUTE, active ownership/branch, shift/workday and source authority guards. SQL replay after branch revocation is denied. |
| Every selected defect requires remediation; optional photos | Browser form rejects whitespace-only notes and highlights missing fields; actual notes and PNG uploaded through HTTPS. SQL rejects missing defect remediation and an invalid second photo. |
| Pending Reinspection and interval closure | Browser Submit returns protected FE04_REWORK_SUBMISSION_V1 receipt. SQL independently checks no open rework interval, expected lifecycle and task audit count. |
| Atomic remediation, evidence, task, interval, audit and intent | SQL fault injection at final audit rolls back all writes and private files. Invalid second upload also cleans the first file. Protected writer stores one completed operation receipt, audit and durable supervisor notification intent in the caller transaction. |
| ETag, CSRF, retry and concurrency | Browser missing-CSRF400, missing-ETag428, stale412, invalid remediation400. Identical replay preserves receipt/publication; changed payload409. Two concurrent SQL submissions produce one durable receipt. Disabled issuer and revoked branch deny replay. |
| Room readiness | Effective block remains BLOCKED. Never-passed fixture follows the explicit first-match GBR-044 rule and remains PENDING_CLEANING while its rework is PENDING_REINSPECTION; no usable state or physical room overwrite. See decision42 for the conflict with UC036 step6. |
| Persisted results and MSG09 | Real reload displays Pending Reinspection, saved remediation, exact private PNG bytes and no Submit action. Confirmed Submit displays “Rework has been submitted and is waiting for reinspection.” |
| In-app supervisor notification | Durable delivery tested with injected recipient failure/rollback, branch revocation, concurrent delivery and retry deduplication. Supervisor logs in through the restricted HTTPS host and sees exactly one submitted notification, including after reload. Uncomposed supervisor source resolution remains404. |

The worker uses the same existing durable audit scan and delivery dispatcher; the trial explicitly invokes delivery rather than claiming a hosted scheduling test. Notification content is minimal. No inspector assignment is inferred from delivery; UC041/047 own the next step.

## Implementation and validation

Application changes: protected submission service/storage, assigned read of verified remediation/evidence, Complete endpoint composition, supervisor notification delivery/dispatcher, canonical per-defect form, command/receipt validation and MSG09. The disposable script adds three remediation fields and a link table using existing keys; generated entities and shared schema are unchanged. Existing source proof and operation-ledger DML denial, CSRF, version, current responsibility and workday guards remain.

Backend and ModelProbe build PASS, zero warnings. `dotnet test HotelOps.slnx` succeeds but the solution itself has no test project. `checkpoint42-regression.txt`: **518 PASS, 11 SKIP**, zero failures. Targeted frontend route/client/source run: **12 PASS**; subsequent client run including the new publication/readiness test: **7 PASS** (overlapping tests, not 19 distinct tests). Typecheck and production build PASS. Full frontend regression initially reported 3256 PASS, 6 FAIL, 1 SKIP (checkpoint42-frontend-tests.txt). Four failures were asynchronous test synchronization races; two retained obsolete Dashboard landing and accepted Users navigation expectations. Test-only corrections preserve authentication and read-only transport assertions. Focused corrected run: 7 PASS. Full rerun: **3262 PASS, 0 FAIL, 1 SKIP** (checkpoint42-frontend-verified.txt). Failed and intermediate receipts are retained; no tests were removed or skipped by these changes.

Existing warnings remain: Vite use-client directives and >500kB bundle, EF unmapped RoomTypeBranchV1 indexes and unordered Take. No dependency or lockfile change; npm ci not repeated. No authentication/security downgrade.

Retained attempts: `checkpoint42-submission-sql.json` failed because EF interpreted JSON braces in the reviewed SQL batch as composite-format placeholders; fixed by verbatim DbCommand execution inside the same transaction. `checkpoint42-submission-sql-rerun.json` passed the unblocked submission suite but timed out on the blocked receive login. Later SQL revocation, full Submit E2E and supervisor-inbox E2E receipts pass. A Windows1909 sandbox process-start failure was bypassed only by the authorized escalated shell; no application or SQL security check was bypassed.

Independent cleanup read: exact server meita/database HotelOps_DEV3_142A_20261114_cc78c13f/GUID 1D5A9B53-7D19-435A-96AB-9FDFF447B6E0; zero users, reworks, attachments and intervals; submission writer, temporary HTTPS role and executor absent. Both owned API hosts stopped. No shared HotelOpsDB or production mutation.

## Remaining workflow and next implementation

Verified chain now reaches **Reject → Request Rework → Receive → Accept → Start → Submit → supervisor inbox**. Full FE05 is still PARTIAL: UC041 exact reinspection assignment, UC047 Fixed/Not Fixed conclusion and next-round Request Rework, current-cycle readiness/history and housekeeper notification remain to compose. Supervisor source links intentionally stay closed until their read authorization and exact binding are implemented. FE11 lifecycle, FE13 and UC006/010 remain in sprint scope.

Next highest-impact implementation: bind UC041 to the exact submitted rework and original defect/checklist version, then implement UC047 Passed/Failed transaction and end-to-end next-round handoff. Next: FE11 remaining lifecycle, then FE13/UC006/010 according to the existing priority correction.

Initial remote for this checkpoint: 08a7ba69fcf3d7addd380ade1b0166f9dc7e3b67. Core staged submission commit: fdb06f8562fa4af36bc019b281dd1f071730867d. Subsequent source/evidence commits and final remote identities are recorded by Git; normal push only.

Final runtime verification was repeated with source commit 6c0ecb6bf21308a009aeb464efb0d1f8a93a7416 recorded by the protected writer registry; both fixtures PASS. Owned private evidence directory contains zero files.
