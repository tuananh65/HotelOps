# DEV3-ENV001-LIVE-001 — focused UC012 handoff

Verdict: PARTIAL. Source-only UC012 normal-flow frontend implemented; live acceptance BLOCKED_BY_TRIAL_LEASE. No UC completion claim from offline tests.

## Baselines and scope

- Worktree: D:/HotelOps-worktrees/dev3-env001; branch feature/dev3-env001-live.
- Accepted source baseline: 29eee0fe1465266b26d5cb837989e1f5f62193dc (integration/fullstack-base; tree identical to PR17 head 8cdb63f).
- Coordination only: bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c (develop / FE04). Not merged.
- FE04 lease: until 2026-11-09 19:17 Asia/Bangkok per Project Leader. Expiry is not authorization. No live request, login, SQL connection, session-touch, fixtures or writes performed.

## Selected implementation

UC DETAIL: docs/requirements/srs_sections/02_use_case_specifications.md, section 2.1 UC-012 (normal flow plus Create/Deactivate alternatives); SRS_CHANGE_NOTES remains pending, so no new policy inferred.

Active contract is Controllers/BranchMaintenanceController.cs -> RoomTypeCreationService.MaintainBranchAsync in Services/HotelStructure/BranchMaintenanceService.cs. DO NOT substitute the inactive BranchAdministrationController/BranchAdministrationService contract. Active ETag is quoted 64-uppercase-hex (SHA256 of row plus audit revision), not base64 SQL rowversion. DTOs: DTOs/HotelStructure/BranchMaintenanceDtos.cs. Existing server requires effective global MASTER_DATA_MANAGE; UI does not infer it.

Implemented normal flow: GET /api/branches/{id}/edit, validated DTO/ETag; edit name/address/phone/email/reason; explicit review/confirm; PUT /api/branches/{id} with current CSRF and exact If-Match. Code/timezone/status are readonly. Optional contact clearing is null. No automatic retry, raw ProblemDetails, storage or logs. Session/branch replacement cancels stale work; ambiguous PUT locks mounted flow even after manual GET; 412 requires a fresh read and a new review. 401/403/404 update rejection clears protected fields.

Route /branches/:branchId/edit and navigation from Branch structure added. No backend change or activation; no FE12 flag change. Create/deactivate alternatives are not implemented here. Branch deactivation remains a separate unresolved writer/dependency gate.

## Focused selection / dependencies

| UC | Existing evidence / scope | Remaining dependency / action |
|---|---|---|
| 004 | Accepted create-user UI/client and initial credential handoff already present | Reuse; no rewrite or completion reclassification without all UC paths |
| 006 | Activation UI/client present; UserAccountDeactivation/README records internal readiness PARTIAL, HTTP blocked | DEV1/TL responsibility gates; no deactivate endpoint invented |
| 007 | UserBranchAssignmentController only POST user branches; accepted Add membership UI | Department/remove/transfer contracts missing; DEV1/TL required |
| 008/009 | No active Role/Permission CRUD controllers in this baseline | Backend owner/TL approved operation/policy contracts; no reopening broad RBAC audit |
| 010 | UserRoleAssignmentController only POST assign; accepted Assign V1 UI | Revoke contract/delegation approval required from DEV1/TL |
| 012 | Active BranchMaintenance GET/POST/PUT and Task74BranchCases source; missing frontend normal flow | Implemented this slice; Create UI, Deactivate dependencies and live acceptance remain |
| 016 | TASK66_UC016_IMPLEMENTATION: PARTIAL; mutation blocked by writer coordination | DEV1/DEV2 retained locks / writer acceptance; no deactivation success claimed |
| 022/026 | TASK70_BATCH1_IMPLEMENTATION records missing deactivation writers/dependencies; no active room/department deactivation endpoint found | DEV1/DEV2 backend contract and orchestration |
| FE12 read | Accepted panel/router/two independent GETs already present | Reuse. Shared integration + approved trial/fixtures for live tests |

## Receipts

- node --test tests/branch-maintenance-api.test.mjs tests/branch-maintenance-browser.test.mjs: 37 PASS (22 API + 15 browser), 0 fail/skip, exit 0.
- Focused cases include actual application route host, independent session admission, 401/403/404/503, branch/session replacement, invalid target, stale ETag, one PUT/double confirm, unknown outcome, no browser storage and no live network.
- Initial verification commands used worktree root instead of frontend and failed ENOENT/no files; corrected working directory. Initial draft used the inactive service's base64 tag; corrected BEFORE final receipt, with explicit base64-rejection regression. No backend changed.
- Typecheck/build/scoped formatting final command receipts reported separately in chat. Build warnings from React Router use-client directives are inherited.

## Live plan — NOT EXECUTED

AUTH-01..08, HOTEL-01..08, TIME-01..10: NOT_RUN / BLOCKED_BY_TRIAL_LEASE. No live PASS receipt reused as a current independent run.

Before any command: runtime owner supplies a dedicated approved origin/CA/cookie jar/identity and lease; verify backend contains the integration source (FE04 develop alone does not). Cookie jars and response files must be private, outside Git/logs. No shared identity. No disabling TLS verification.

Prepared PowerShell GET templates (variables supplied by authorized owner; not current FE04 addresses):

```powershell
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/auth/session"
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --cookie "$ApprovedCookieJar" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/auth/session"
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --cookie "$ApprovedCookieJar" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/branches/$BranchId/structure"
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --cookie "$ApprovedCookieJar" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/branches/$BranchId/edit"
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --cookie "$ApprovedCookieJar" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/branches/$BranchId/workdays/own-shifts"
curl.exe --proto '=https' --cacert "$ApprovedCaPem" --cookie "$ApprovedCookieJar" --dump-header "$PrivateHeaderFile" --output "$PrivateResponseFile" --write-out '%{http_code}' "$ApprovedOrigin/api/branches/$BranchId/workdays/current"
```

Do not run GET templates without lease: session-touch writes remain possible. Login/logout require separate approved session-mutation identity/lease. UC012 PUT requires explicit disposable write-trial approval, review/If-Match and dedicated fixture; no write template executed. TIME-02/04/05 require specific workday/grant fixtures; mark FIXTURE_UNAVAILABLE if absent, never change FE04 fixtures. TIME-09 route safety may be checked using MVC metadata without POST probes.

## Handoff / acceptance boundaries

Proposed source-only commit: UC012 branch metadata edit frontend + focused tests + this handoff. No commit/push/PR authorized or performed.

UC012 has three documented flow families (normal update, create, deactivate). This slice adds frontend normal-update coverage, not the other two. Source/UI activation on a deployed integration and SQL audit/rollback acceptance remain unverified; whole-UC completion percentage is not established. Live verified coverage in this task: 0/3 UC012 flow families, 0/26 requested AUTH/HOTEL/TIME smoke cases. No 100% claim.

To combine FE04 later, TL/DEV1 must approve entry/router composition (frontend/index.html, frontend/src/main.tsx) and Program.cs DI/resource-resolver/interceptor composition, retaining both accepted baselines. No wholesale cherry-pick, merge, overwrite or shared runtime deployment in this task.
