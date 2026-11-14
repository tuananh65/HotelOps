# FASTTRACK-142A checkpoint 05 — scoped audit API and canonical UI

Baseline `3e973de8ff0c9ec2cf9c087be3866760aa5608d1`, canonical integration branch. Sprint active; no whole-UC completion claim.

## Implemented

Selectively recovered AuditLogsController, InsightErrors, InsightContracts, InsightAccess, AuditReadService and AuditRedaction from `origin/feature/DEV2-fe14-fe15-scoped-review` (review pin `4ce3c470`). Registered only audit/access services. GET `/api/audit-logs`, `/api/audit-logs/options` and `/api/audit-logs/{id}` now support scoped list/filter/pagination and redacted detail using existing AUDIT_VIEW permission. The detail read was strengthened to keep authorization and data retrieval in one SERIALIZABLE read transaction, matching the candidate list behavior. No audit mutation endpoint, new permission, role assignment policy or EF schema change.

UC136/137 requirements checked in SRS sections 15.2/15.3 and actual AuditLog mappings. Department filtering is explicitly unavailable because audit rows have no department scope; it returns 400 instead of guessing. Branch-scoped users cannot see foreign or global audit rows. Unknown JSON keys, nested values and malformed payloads are withheld; no raw errors or secret-bearing payload snippets are sent to the UI.

Added canonical `/audit` route and navigation, list filters, pagination, detail and before/after view. Data is bound to authority generation and current request; cancellation and generation changes suppress late responses. HTTP 401 invalidates only the captured current authority, with no automatic business replay. Route admission never grants AUDIT_VIEW.

FE05 investigation: current candidate projection requires a registered issuer and a different notification graph. The reviewed issuer supports Reject/Rework, not Approve, and notification staging explicitly throws while the durable graph is unavailable. No guard was removed. This independent FE15 slice advances the sprint while those composition contracts remain unfinished.

## Direct validation

| Validation | Actual result | Evidence |
|---|---|---|
| Backend build | PASS, 0 warnings/errors | `checkpoint05-build.log.txt` |
| Audit redaction xUnit | 7 PASS | `checkpoint05-audit-tests.log.txt` |
| Solution test command | exit 0, no discovered tests; not suite acceptance | `checkpoint05-solution-test.log.txt` |
| HTTPS/SQL auth plus audit trial | 24 checks PASS; cleanup true | `audit-https-first.json` |
| TypeScript | PASS | `checkpoint05-typecheck-final.log.txt` |
| Frontend production build | PASS | `checkpoint05-frontend-build-verified.log.txt` |
| Auth/Router browser-backed tests | 122 PASS, 0 FAIL/SKIP | `checkpoint05-auth-router.log.txt` |

HTTPS uses normal trusted localhost certificate validation and the real built API. Trial starts with no grant (403), creates synthetic branches and existing SYS_ADMIN role assignment scoped only to one branch, verifies authorized list/detail and redaction, foreign/global detail 404, foreign branch 403, invalid filter/page/date 400, exact record filter, and revoked grant 403. Existing auth checks cover login/session/logout/CSRF. Secrets remain in memory; the literal SYNTHETIC_SECRET is only a test sentinel. HTTP fixtures commit by design and are removed by exact fixture-scoped compensating transactions; users/sessions/audits/branches/grants are verified clean. No durable business workflow completion is inferred.

Target remains `meita` / `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, run marker `cc78c13ffc124efcad67b3d804baa84c`. Existing exact-target auth harness verifies identity before mutation. Shared HotelOpsDB/ImportReview untouched. API process was stopped after the trial.

Browser tests mount the canonical React host with local mocked HTTP, including list/detail, session-state denials, late response suppression and 401 invalidation. They are not browser-to-real-SQL E2E. The initial two-file run had 121 PASS; final added 401 case produced 122 PASS. npm ci was not rerun because lockfile/dependencies are unchanged from accepted baseline.

Environment observations retained: PowerShell blocked npm.ps1; npm.cmd used without policy changes. First production build wrapper reported nonzero due to PowerShell stderr handling despite Vite producing output; final cmd-wrapped build returned exit 0. React Router `use client` and bundle >500 kB warnings persist. Browser bundler has existing import.meta/iife warning; runtime retains existing RoomTypeBranchV1 mapping warnings. New logs are UTF-8 with trailing display padding removed only.

## Receipt

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=05_ACTIVE`; `INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`; `ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114`.

`SOURCE_IMPLEMENTED=SCOPED_AUDIT_READ_API_AND_CANONICAL_UI`; changed paths are audit controllers/DTO/services, Program DI, frontend audit module/AuthRouteHost/SharedAuthHost, focused tests and ModelProbe audit scenario/evidence.

`FE05_STATUS=PARTIAL; APPROVAL_COMPOSITION_PENDING`; `UC006_UC010_STATUS=WRITER_COORDINATION_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=CANDIDATE_PENDING`; `FE15_STATUS=AUDIT_READ_API_UI_IMPLEMENTED; REPORTS_AND_FULL_ACCEPTANCE_PENDING`.

`ISOLATED_SQL_STATUS=AUDIT_SCOPE_REDACTION_AND_CLEANUP_PASS`; `HTTPS_API_STATUS=AUTH_AND_AUDIT_24_CHECKS_PASS;STOPPED`; `E2E_VERIFIED_WORKFLOWS=API_SQL_AUTH_AND_AUDIT_SLICE;BROWSER_REAL_SQL_NOT_RUN`.

`UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`.

`CRITICAL_BLOCKERS=FE05_NOTIFICATION_ISSUER_COMPOSITION;REINSPECTION_STORAGE;WRITER_RACES;MISSING_FE11_13_AND_FE14_REPORT_WORKFLOWS;FULL_ACCEPTANCE`.

`NEXT_IMPLEMENTATION=Resolve FE05 issuer/notification/projection composition; verify UC006/010 competing SQL writers; complete FE11/13 and FE14/report integration`. UC136/137 still need broader global/record-history acceptance and real browser-to-SQL testing before COMPLETE_VERIFIED.
