# Separate Tester environment

Requirements: Windows, .NET SDK10, Node24 + locked npm dependencies, Edge, local SQLEXPRESS and an existing trusted ASP.NET localhost development certificate. The setup uses a NEW database HotelOps_Canonical_20261106_f11tester_Fresh with owner DEV1_FS011Tester; it never resets/overwrites FS010, FS011 development or other databases. Never run developer fault suites on historical evidence. Provisioning is restricted to the documented local development/trial machine; SQL integrated authentication must already be authorized.

1. Obtain/check out the repository CODE_COMMIT.txt or a documentation-only descendant. Install dependencies from package-lock if absent. Validate SOURCE_MANIFEST with SETUP_TESTER.ps1.
2. Plan: powershell -NoProfile -File SETUP_TESTER.ps1 -RepositoryRoot D:\SEP490\Projects\HotelOps-DEV1
3. Create NEW trial once: same command with -Execute. Existing scaffold/database refuses overwrite. If an incomplete setup remains, preserve it and contact developer; never drop/reset automatically.
4. Start: powershell -NoProfile -File <repo>/scripts/verification/FS011Tester/Start-LiveEnvironment.ps1 -StartApprovedTrial
5. UI https://localhost:5173, API https://localhost:7443. Owned Vite loopback5189. Stop any development-owned environment first using its Stop script; never kill by port. Start/stop scripts check owner/PID/start identity. Do not build while owned binaries run.
6. Manual authenticated browser without sharing passwords: set NODE_EXTRA_CA_CERTS to <repo>/scripts/verification/FS011Tester/.private/localhost-public.pem; run node Open-TesterBrowser.cjs <repo> admin (or owner/other/denied). The explicit launcher opens Edge, signs into the actual SQL-backed API and retains cookies only in memory. No HAR/trace/storageState/token/password file export. Close the browser to end its session.
7. Stop: powershell -NoProfile -File <repo>/scripts/verification/FS011Tester/Stop-LiveEnvironment.ps1

Synthetic names fs011tester.admin, .owner, .other, .denied. Roles/grants are looked up from existing catalog, never hard-coded numeric IDs. Read branch IDs from local fixture; obtain room/type/priority from options. B1 rooms FS011Tester-B1-001..025; room025 is deliberately inactive, 001/002 are progress/source fixtures; choose unused ACTIVE room003 onward for Create. Shift eligibility is time-limited: revalidate on another day, do not alter evidence to force PASS.

Candidate flag FS011Tester_PROVISIONAL requires Development + exact database/owner/version/local instance. Default flag OFF returns503/no assignment. Start with -DisableCandidate only after stopping owned processes to execute default-off case.

Tester helper allows plan/gateway/credentials private IPC/schema/seed/read-only inspection/explicit delivery only. Fault/revoke/supersede/commit-proof modes reject TESTER_FAULT_MODE_DENIED before SQL connection. Raw admin SQL is still privileged; this is no SQL sandbox. Do not execute developer SQL/fault scripts or recovery/drop on this environment. Reuse existing trial to continue testing; no reseed/reset. Credentials remain local DPAPI under private ignored folder, never included here.
