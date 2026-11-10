# FE05-INT001 - Live checklist integration verification

Verdict: PARTIALLY_VERIFIED. Feature flows have live receipts; the shared frontend Sign-In screen is still a mock. No UC039/UC040 ACCEPTED or shared deployment claim.

## Runtime identity

- Local machine/SQL instance: DESKTOP-JI8U50T / SQLEXPRESS, SQL Server 16.0.1000.6.
- Database: `HotelOps_Canonical_20261108_024451_a6ccd3a9_Fresh`.
- Owner: `HotelOps.FE05INT001.Owner=5575291d-51a8-4d8a-88fd-abe8bbd87aeb`.
- New schema-only trial with synthetic data, 0 users before fixture setup, file caps 256/128 MiB. No backup, shared configuration credentials or production data was imported. Original INSP-004C3 and all diagnostic trial databases remain retained.
- API: real ASP.NET Core/.NET 10 at `https://127.0.0.1:7447`.
- Frontend: existing UI003 production build through test-only HTTPS static server/reverse proxy at `https://127.0.0.1:7448`, local Edge. Upstream proxy trusts the generated local certificate; browser allows its untrusted chain in this test context only. No trust-store changes or shared routing changes.
- Backend worktree/branch: HotelOps-DEV1-FE05-COLLECTION / feature/dev1-fe05-checklist-collection, HEAD 6aee2a0e692a7814f41c81224709cddfbe1e2ec4 plus existing UI002 changes.
- Frontend worktree/branch: HotelOps-DEV1-FE05-UI / feature/dev1-fe05-checklist-ui, HEAD 9d220a94509e650464406a9b83099d24cb568220 plus existing UI001/UI003 changes.

## Safety and prerequisite evidence

Every fixture write uses exact instance/database/owner checks. Each unsafe proxied request runs a read-only ownership probe before forwarding to the real backend. Real application transactions and permission checks are unchanged. No shared SQL, migration, D4, UC045, FE04, DEV2/DEV3 production source or endpoint flags changed.

The existing schema/catalog/canonical seed, UC017 and checklist trial installers were reused. The existing guarded Task26 `scripts/verification/UserProfileUpdate/rowversion-forward.sql` was required for the current binary's users.row_version startup gate. No SQL source was modified.

Synthetic grants follow the accepted Task72 isolated fixture: existing HK_SUPERVISOR + existing CHECKLIST_MANAGE/BRANCH, user assignment and access for branch 1 only. This is not proof of accepted shared role provisioning. MASTER_DATA_MANAGE was NOT granted to HK. Another synthetic credentialed user has no role grants.

## Results and receipt provenance

Initial live run: 9 PASS, one composite Publish-v2/Retire timeout, five later cases unstarted. A later read-only SQL probe established that the v2 publish actually persisted. The original failed assertion and original receipt are retained; it is not rewritten as a clean run.

Continuation restarts the same trial without reseeding/resetting. It confirms version 2 PUBLISHED through the actual frontend, retires it through UI, reloads and checks preserved historical criteria. It runs only remaining verification and necessary real login/readiness setup. No HTTP interception or mock session is used.

The harness was corrected to use asynchronous ownership probes (avoids blocking its Node HTTPS proxy), wait for the actual POST response instead of an earlier success notice, allow a live-operation timeout of 120s and close owned proxy connections on exit. These are verification corrections; no application source change was required. Persisted evidence establishes publication success; the initial timeout alone is not proof of a production defect or latency SLA compliance.

Final continuation: **7/7 PASS**, runner exit 0; resumed backend shutdown exit 0. Original 9 PASS / 1 timeout remain recorded separately. All planned feature scenarios now have supporting live evidence across these runs; no single clean combined run is claimed.

| Scenario | Actual final evidence |
| --- | --- |
| Real credentials / backend cookie session | PASS; /api/auth/login and /session, Secure + HttpOnly + SameSite=Lax |
| Collection loading | PASS; real SQL query and frontend display |
| Search / active / historical-status / RoomType / server pagination | PASS; 21 branch-owned roots, pages 1/2 and 2/2, combined filters return main record |
| Create / edit Draft | PASS through real frontend |
| Stale ETag | PASS; concurrent real PUT, UI receives 412, blocks save, reload recovers |
| Publish v1 / create next Draft / Publish v2 | PASS persisted state; v2 observed after original timeout, no duplicate publish/replay |
| Retire / refresh / reload | PASS through UI with actual 200 response; both versions remain RETIRED |
| Historical protection | PASS used-version UI and SQL content/FK; PENDING synthetic Inspection fixture only |
| Missing CSRF | PASS 400; no INT_NO_CSRF row inserted |
| Foreign branch / denied real-login user / anonymous | PASS 403 / 403 / 401; frontend forbidden state verified |
| Independent SQL persistence | PASS; version IDs 2/3, unchanged criteria, InspectionId 1 still points to version 2; 3 persisted session rows, 27 checklist audit rows |

`run03/resume01/live-results.json` SHA256: `D0211FC2803F5B1C28EEE2A47514121FE70583394636D246696C1E5CA9104881`.

`run03/resume01/persistence.json` SHA256: `15F4FCF2384E7C8854DB87A3AED3A153615772C9D5A2B22B23530B7CAD12F376`.

These receipts are under ignored `TestResults/fe05-int001/` in the original INSP worktree. Both are sanitized; do not share runtime.private files alongside them.

SQL persistence checks independently query versions/criteria, Inspection FK, real user session rows, checklist audit count and absence of a no-CSRF insert. The historical association is an explicit synthetic PENDING Inspection fixture, not UC041 assignment or completed Inspection acceptance.

## Authentication/RoomType/shared dependencies

Backend login and session are real, with actual password hashing, SQL session, encrypted cookie and CSRF. The runner calls `/api/auth/login` from the browser; it does not inject session cookies or bypass authorization.

Shared `frontend/src/screens/auth/SignInScreen.tsx` still calls `frontend/src/services.ts:login`, which returns mock currentUser. The Sign-In UI is NOT verified. DEV3 needs to bind the existing shared form/session/logout to backend auth/CSRF and provide accepted HTTPS routing. This task does not implement replacement auth or router changes.

RoomType GET returns actual 403 for the HK fixture because it requires MASTER_DATA_MANAGE. The checklist form's known-ID fallback works, but a normal checklist-readable RoomType selector contract remains a shared decision. Do not resolve this by granting HK MASTER_DATA_MANAGE.

The local integrated SQL identity can create databases and is more privileged than a production runtime principal. These tests do not certify least-privilege deployment, shared grants, load/performance, complete inspection lifecycle or production certificate handling.

## New files in this task

Backend:

- `scripts/verification/ChecklistLive/ChecklistLive.csproj`
- `scripts/verification/ChecklistLive/Program.cs`
- `scripts/verification/ChecklistLive/README.md`
- `docs/fe05/FE05_INT001_LIVE_REPORT.md`

Frontend:

- `frontend/tests/fe05-live.mjs`
- `frontend/tests/fe05-live-resume.mjs`

Only test infrastructure/documentation was added. Previously uncommitted production changes remain intact. Build of the verification project and referenced API: PASS, 0 warnings/0 errors. Manual browser runner syntax checks: PASS. Unchanged frontend build/focused tests/historical assertions were not rerun.

Setup scripts and raw local receipts are ignored under `TestResults/fe05-int001/`. `runtime.private.*` includes synthetic password and private TLS artifacts and MUST NOT be committed/shared. Public environment/runtime/results/persistence receipts contain no password or connection string. No database is dropped/reset; no commit/push/merge/deploy.

## Diagnostic history

run01 retained the missing users.row_version prerequisite and invalid RoomType default fixture attempts. run02 retained a setup refusal when generic retargeting would have changed the Task26 forbidden-database guard. Setup was corrected to preserve that existing script verbatim; run03 has a valid generic published bootstrap checklist and proper auth prerequisite. These were fixture/setup errors, not application fixes.

An automatic approval review temporarily failed because the tool account token could not refresh. The diagnostic command was not executed, work stopped, and owned runtimes shut down through the existing stop-file mechanism. After the user requested continuation, a newly approved read-only probe succeeded; verification resumed without bypassing approval or resetting data.
## Final progress and handoff

UC039 and UC040 are each estimated at approximately 90% after this verification (engineering estimate, not a completion ledger change). Remaining: accepted shared Sign-In/session binding and HTTPS transport, normal-user permission provisioning, approved RoomType read usability, independent review. There is no confirmed application source defect from these tests; the original timeout was a harness assertion failure with subsequently confirmed persisted publication.

The API and HTTPS proxy created by this task were stopped; trial databases remain retained. `git diff --check` passed in both worktrees. No application production source changed during INT001 and all earlier uncommitted files remain. No commit/push/merge/deploy or shared SQL execution occurred.