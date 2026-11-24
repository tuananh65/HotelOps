# P0 local Production deployment — 24 November 2026

Status: **VERIFIED deployment**, **PARTIAL full A+B acceptance**. User explicitly requested “cập nhật cả production”. Earlier reports that Production was unchanged are historical and superseded by this record.

## Target and running processes

- Project 2 only; Production URL https://127.0.0.1:5080/.
- Database: HotelOpsCompactDB, schema 3.0.0; 49 tables, 205 foreign keys, 97 check constraints, 38 triggers. Constraints trusted/enabled.
- API: Production environment, https://localhost:5769, PID43404, Release `artifacts/production-261124-p0/api/HotelOps.Api.dll`.
- Frontend: PID7680, launcher PID10388, `artifacts/production-261124-p0/frontend`; bundle `index-BKLWzwFk.js`.
- TEST API5774 PID15136 and frontend5174 PID48136 preserved. Rehearsal API5775 PID45248 stopped after verification.
- Existing worker setting remains false; shift expiry remains enabled. HK shift closure follows the new manual completion/return policy.

## Backup and migration evidence

1. COPY_ONLY/CHECKSUM backup at 15:53, VERIFYONLY and actual restore to `HotelOpsCompactWorkdayTestP0Prod2611241555` passed.
2. All nine P0 SQL migrations committed on that clone. Release API started successfully against it; health reported 3.0.0 and 49 tables.
3. The exact-target Production runner was rehearsed against the clone with rollback, all nine script hashes recorded. Initial parameterized CREATE VIEW batch failed and rolled back; passing retry only binds the target parameter in batches containing the target guard. No business SQL was changed.
4. Old Production API/frontend stopped. Final verified backup: `HotelOpsCompactDB_before_workday_20261124_160215262.bak` in the SQL Server backup directory recorded in `backup-final.json`.
5. Migration committed at 2026-11-24T09:02:28Z. User IDs, usernames, password hashes/security stamps, stock movement rows and existing work/audit history IDs were checked for preservation within the transaction.
6. Primary database has 57 users, eight active roles, zero active assignments to inactive legacy roles and zero p0-prefixed TEST users. Migration creates configuration metadata, not fabricated stock.

Evidence under `artifacts/production-261124-p0/`: `backup-rehearsal.json`, `backup-final.json`, `migration-rehearsal.json`, `migration-production.json`, `database-verification.json`, `runtime.json`, `verification.json`, `login.png` and API logs. SQL hashes and API DLL hash are recorded there; no credentials are included.

## Validation and limits

- Release API publish passed. Frontend deployment build produced the deployed bundle.
- Real Edge browser smoke passed with trusted TLS: HTTP200, expected JS bundle HTTP200, login visible, anonymous `/api/v2/auth/me` returns 401, zero page runtime errors.
- API health passed: schema3.0.0, tables49; API error log empty at verification.
- Existing TEST evidence remains in `validation-evidence.md`: 98 unit tests passed, three existing integration cases skipped, cumulative 28 runtime scenario groups and browser route/layout evidence. These were not rerun as mutating workflows on Production.
- No existing employee credentials were used for an authenticated Production business-flow test. Users must log in again after role/session migration.
- Full A+B acceptance remains PARTIAL: unified creation/dynamic form coverage and criterion-level remaining acceptance mapping are still open. Deployment does not claim those are complete.

## Files introduced or updated for deployment

- `scripts/database/compact-v2/Apply-P0Production.ps1`: exact database target, fresh backup, matching migration hashes, rehearsal evidence, API stopped guard, transactional preservation and constraint checks.
- `frontend/tests/verify-p0-production.mjs`: trusted HTTPS deployment smoke.
- `plans/261124-1237-p0-workflow/plan.md`: current authorization and deployment checkpoint.
- This deployment record; local launcher/build/evidence artifacts in the release directory.

No C# source changes in this deployment. No commit, push or merge performed.

## Recovery

Keep both backups and both release directories. Do not launch the old 2.1 API against the migrated 3.0 database. Prefer forward repair using the verified new Release. A database rollback would require stopping Production writers, backing up post-cutover data, restoring the recorded pre-cutover backup and reconciling any newer business activity before restarting the old API/frontend. No rollback/restore-over-primary was executed. The original migration scripts remain TEST-target protected; the Production runner admits only the explicitly named primary after matching rehearsal proof.
