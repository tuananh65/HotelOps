# FE05-INT001 live verification harness

This is manual test infrastructure, outside the application solution. It does not implement authentication, checklist business logic or a production endpoint. It references the current API project and starts the real compiled ASP.NET Core process.

Requires a NEW explicitly authorized isolated trial, synthetic fixtures, local reviewed SQL instance and a guarded creation receipt. Do not point it at shared/production SQL. No reset/drop operation is provided. Every SQL fixture write and unsafe proxied request checks the exact instance/database/owner marker; real application writes execute in existing service transactions.

Modes:

- `probe <environment.json> <output>`: read-only identity and ownership check.
- `serve <environment.json> <output>`: refuses existing personnel/types, adds synthetic fixtures and starts HTTPS API.
- `resume <environment.json> <new-output>`: reuses existing synthetic credentials from the original receipt directory without reseeding/resetting. Generates fresh local TLS artifacts and starts API. Use a new output directory with no stop file.
- `bind <environment.json> <output>`: creates one synthetic PENDING Inspection association to verify used-version protection; refuses an existing association. Not UC041/UC045 acceptance.
- `verify <environment.json> <output>`: read-only final persistence checks for this exact scenario.

The owned API stops when `<output>/stop` is present or the 15-minute harness deadline expires. It does not delete trial databases. `serve`/`resume` write generated PRIVATE password/certificate/key files only into the ignored local output directory. Never commit or share those files. Public runtime/environment/results JSON exclude credentials and connection strings.

Frontend manual runners:

- `frontend/tests/fe05-live.mjs <output>`: fresh scenario, including Create/Edit/stale/Publish, historical binding, next Draft, Retire, filters/pages and negative checks.
- `frontend/tests/fe05-live-resume.mjs <new-output> <original-environment.json>`: targeted continuation on the retained Published-v2 state from the original INT001 run. Refuses to simulate earlier success; it reads actual persisted state.

These serve the existing frontend `dist/` via a test-only HTTPS reverse proxy and launch Edge. No HTTP interception, mock session or fake response is used for positive verification. Proxy-generated 502/503 is infrastructure failure, never a backend success. SQL ownership probes run asynchronously to avoid blocking the proxy. Mutation assertions wait for the actual POST response and subsequent UI, not a notice left by an earlier operation. Tests allow up to 120s for live operations; this is not a latency SLA.

Real backend login is invoked from the browser with a synthetic password and CSRF. Shared frontend SignInScreen remains a prototype and is NOT certified by these tests. Existing Task72 isolated CHECKLIST_MANAGE/BRANCH fixture is used; MASTER_DATA_MANAGE is never granted to HK. Normal permission provisioning and RoomType read-contract acceptance remain external dependencies.

Do not run these scripts as part of general test discovery. Do not rerun them on a populated trial as if it were clean. Review the original and continuation receipts together; do not claim a single clean combined run. Existing scripts/source/schema are not merged, copied or patched to bypass guards.