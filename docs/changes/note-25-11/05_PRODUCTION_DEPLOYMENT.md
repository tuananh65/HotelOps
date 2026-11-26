# Production local — Note25_11,26/11/2026

Owner explicitly authorized applying the verified changes to HotelOpsCompactDB and Production local. This supersedes the earlier receipt's deployment-pending status. No EC2/Git deployment was performed.

- URL:https://localhost:5080; database:HotelOpsCompactDB;57tables; health PASS.
- Fresh COPY_ONLY/CHECKSUM backup and RESTORE VERIFYONLY completed before patch. Backup:`C:/Program Files/Microsoft SQL Server/MSSQL16.SQLEXPRESS/MSSQL/Backup/HotelOpsCompactDB_before_workday_20261126_065854430.bak`.
- Applied `scripts/database/compact-v2/note-25-11-refill-om.sql`. Constraint trusted/enabled; no legacy pending refill. All table row counts except expected audit/notification additions were preserved. No restore/reset or stock adjustment.
- Release API and frontend built into a staging directory, then switched while Production was stopped. Previous release retained at `.artifacts/production-before-note2511-20261126-065902`.
- Published API SHA256 matches current Release DLL. Backend build0warnings/0errors; tests197passed/11environment-skipped. Frontend build/typecheck PASS. Publish retried a locked OFL font file successfully; Vite reported existing SignalR PURE annotations and external output-directory notice.
- Actual Edge browser smoke PASS on Production and Development with TLS validation enabled:login/password toggle/mobile recovery, static JavaScript, anonymous API401, unknown API404, realtime anonymous401. No real password reset/email or test business transactions on official DB.
- Both local runtimes healthy. Development remains isolated at5173/API5081. AI and real email retain their disabled configuration.

Evidence:`artifacts/local-runtime/note2511-production-database.json`, `artifacts/workday/backup-HotelOpsCompactDB.json`, `artifacts/local-runtime/browser-smoke.json`. Migration runner:`scripts/local/apply-note2511-production.ps1`.

Earlier limits remain:legacy frontend suite timeout and untested network-disconnect SignalR scenarios. SQL business integration tests were previously run on isolated database; production smoke is not a claim of replaying all business workflows on live data. One sandbox PowerShell HTTPS probe failed; elevated runtime health and real Edge TLS checks passed.

Rollback:stop only Production using the owned-process launcher and restore the preserved release directory if needed. The forward schema retains the legacy refill status; already rerouted requests require explicit reconciliation if rolling back workflow semantics. Do not automatically restore the DB backup over newer business data.
