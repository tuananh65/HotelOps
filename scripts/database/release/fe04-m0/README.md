# FASTTRACK-109A — FE04 M0 isolated single-target source candidate

Status: **SOURCE CANDIDATE ONLY.** No Gate B/Gate C execution is granted. The patch adds files; no existing installer, Auth/Router or production service is changed. Repository baseline: `084d06596d5026a050c18bc47df19942114755de`.

## Exact target and capability

- Operator-approved candidate: `DESKTOP-JI8U50T\SQLEXPRESS`, database `HotelOps_FE04_Trial_20261113_ENV001_U107_7e94a2bf` (name only, not created/reserved).
- `Invoke-FE04M0SingleTarget.ps1` has `GuardCheck` (contract and all local artifact hashes), `Plan` (local source SHA256 observations), `Inspect` (strict-TLS read-only SQL host/collision check), `InstallCore` (requires **detached PL signature** and pinned PL public certificate plus all accepted artifact hashes), `Verify` (read-only SQL GUID/owner/marker/minimum schema). Dot-sourcing imports definitions without dispatch. Windows PowerShell 5.1 and PowerShell 7 are supported; native offline validation must state the actual version used.
- **B2/B3 only:** at most one `CREATE DATABASE` from source-pinned Full Schema, then catalog, accepted patches, canonical contract/seed, read-only RBAC verify and the Task26 rowversion prerequisite. The full canonical verifier (82-table/9-rowversion expectation) is explicitly NOT executed in the minimal M0 path; its source hash remains pinned for provenance. A separate M0-only read-only schema oracle verifies mandatory tables/catalog/Task26 and is independently reviewed via the signed approval flag `m0_minimum_schema_verifier_accepted`. This is NOT full canonical acceptance. No standalone grants, synthetic fixture DML, HTTPS host, or Gate C execution.
- On `InstallCore`, **no database may already exist**, including a colliding candidate. It holds a session `sp_getapplock` on `master`, rechecks collision before create, uses a **strict TLS** connection with `Encrypt=True`, `TrustServerCertificate=False` and the expected SQL hostname. It never invokes the three-target canonical trial runner.
- `01_HotelOps_Full_Schema.sql` requires in-memory substitution of an exact `HotelOpsDB` guard. The RBAC catalog + four patches similarly have fixed database-name checks. Canonical scripts require an exact-target-and-owner-marker guard adapter. Task26 is located at `scripts/verification/UserProfileUpdate/rowversion-forward.sql` (Git blob `f6592581e45f712360f6b24663b7fc4c967fa9fc`) and is adapted **only after separate explicit owner review**, preserving the rowversion change while strengthening FE04 target/marker verification. All source bytes are SHA256-pinned before any operation.
- **Install sequence matters:** run exact full schema, RBAC, four reviewed patches, canonical reference/seed and read-only catalog verification before Task26. Do NOT run the full canonical verifier: it requires a different 82-table/9-rowversion composition; the M0 verifier is narrower and has an explicit signed owner-review gate. The final Task26/StartupSchema metadata check runs last.
- Failure/unknown outcome: save a sanitized receipt where possible and stop. **Never retry, overwrite, DROP, reset, or auto-cleanup**. The deployment owner reconciles the live GUID, marker, schema, and artifacts with a separate connection and obtains explicit cleanup permission.

## Offline source checks (no SQL)

```powershell
powershell -NoProfile -File scripts/verification/FE04M0/Test-FE04M0InstallerGuards.ps1
powershell -NoProfile -File scripts/database/release/fe04-m0/Invoke-FE04M0SingleTarget.ps1 -Stage GuardCheck
powershell -NoProfile -File scripts/database/release/fe04-m0/Invoke-FE04M0SingleTarget.ps1 -Stage Plan
```

`Plan` prints observed hashes; it **does not** approve them. All 11 source SHA256 values were calculated from GitHub content and independently checked against Git blobs at the pinned commit. They are **source identities, not owner acceptance**; DEV1 must verify bytes again locally and explicitly accept each artifact before provisioning. The detached Project Leader envelope cannot be forged using JSON booleans: installer requires a **pinned source-level public certificate fingerprint**, a matching public certificate, a detached RSA/SHA256 signature over the exact Gate B bytes, and an operation/target/source/hash/time-bound envelope. The approval envelope must also attest the accepted FE04 composition, FE04 and ENV source signoffs, and the FE04 Task26 adapter decision and explicit acceptance of the M0-only schema verifier (not full canonical acceptance). The public certificate fingerprint is deliberately `PENDING_PROJECT_LEADER_CERT_PIN` until approval; therefore `InstallCore` is blocked in this candidate.

## Gate B approvals still required

1. The four-file source allowlist and accepted FE04 source baseline are already granted by Master. Source acceptance of the final installer hashes, Task26 FE04 guard adapter and module order still precedes provisioning; DEV3 is consulted for FE01/RBAC contract only if needed.
2. Actual Project Leader/authorized approver: signed envelope scope **B2 + B3 only** after source review. B4 restricted app/observer grants; B5 fixture seed; B6 HTTPS config; B7 issuance of exclusive lease; B8 cleanup/retention require their **own** exact authorizations. A signed B2/B3 envelope does not authorize these.
3. M0 user fixture manifest: synthetic supervisor with `CLEANING_VIEW` branch authority, denied/wrong-branch actor, branch/HK department, optional task/room/assignment. Only real generated IDs after authorized seed. If no task, list may be empty and detail is SKIPPED_FIXTURE_ABSENT, not PASS.
4. HTTPS origin/certificate with CA-chain and SAN; application binary SHA256 and target GUID binding after provisioning; separate restricted app and observer SQL principals (operator `hax` is NOT application/observer acceptance principal); exclusive fresh lease; evidence protected ACL and retention.

## Gate C — not yet authorized

After separately approved B1–B8 provisioning, execute only approved API/SPA HTTPS startup, `/api/health/database`, `POST /api/auth/login`, `GET /api/auth/session`, `GET /api/branches/{branchId}/cleaning-monitor` and optional `GET /api/branches/{branchId}/cleaning-monitor/cleaning/{taskId}`, negative 401/403, independent SQL read-only fingerprints and shutdown/reconciliation. The health endpoint alone does **not** attest DB GUID. No UC028 create, UC034 cases, UC038 route activation or Notification/Rework writer.

## Windows implementation hardening

The supplied patch was applied once, then corrected only within its four new files.
The full-schema adapter preserves both original collision/context checks and changes
their NOEXEC aborts to THROW; it verifies exactly one CREATE statement. It records
the actual GUID immediately after CREATE succeeds, before later schema batches.
The signed Gate B envelope must also bind `setup_operator` and uppercase
`database_owner_sid_hex`; the installer verifies SQL operator/creator SID and the
database owner independently of the random trial marker. GUID/owner/marker/expiry
are rechecked before each later module. Partial failures retain any already observed
GUID/marker and never drop, repair or retry. Database creation and module commits
are not one atomic transaction. Master-scoped application locks coordinate only
participating installers; all-client exclusion remains an external prerequisite.

GuardCheck enforces all eleven artifact hashes; Plan only observes them. Offline
tests retain the fifteen supplied cases and add thirteen rejection cases for hash
omission/drift, string TLS booleans, collisions, owner/marker/server/GUID mismatch,
missing independent trust anchor/prerequisite file, changed Task26/canonical guards and expiry.
Three positive adapter checks preserve the complete Task26 schema tail, prove a
single exact CREATE with abort guards and preserve canonical target/marker scope:
31 offline cases in total. The same commands can also be run with pwsh on PowerShell 7.
These test doubles exercise guard decisions, not SQL/TLS acceptance. No InstallCore,
Inspect or Verify invocation belongs to the source-only validation commands.

Task26 adaptation and the NOEXEC-to-THROW adapter still require Schema/Security
source acceptance through signed `canonical_fe04_guard_adapter_accepted` and
`full_schema_abort_adapter_accepted` envelope fields, in addition to the existing
Task26/verifier acceptance fields. The minimum verifier covers the current StartupSchemaValidator
users.row_version requirement; it is not full mapping/catalog/writer certification.
The real PL certificate pin remains a non-executable placeholder. No reviewer may
set it from a self-declared JSON flag or infer approval from these offline results.

## Current limitations

- Native offline execution results are recorded in the ENV delivery receipt. SQL/TLS handshake, installed schema, actual rollback and HTTPS runtime remain NOT_RUN. The source adapter does not inherit the old canonical-only module runner approval.
- The installer only implements B2/B3 as a source candidate. It does NOT implement or pretend to complete B4–B8 or Gate C.
- The first local `Plan` and guard tests do not prove SQL schema compatibility or an application/observer identity. If any of these fail, do not try to loosen validators: present the exact failing guard and owner scope to DEV1 Master.

## FASTTRACK-108B + 110B owner handoff

FE04 accepted source-only composition `084d06596d5026a050c18bc47df19942114755de` for UC037 M0 (eight exact source blobs). PR97 remains outside M0. Actual deployed API/SPA binaries and their SHA256 require a separate post-provision attestation; setting the source commit does not confer deployment or operation approval.

FASTTRACK-110B accepted the FE05 UC034 completion source contract (cleaner attribution/inspection handoff) at four unchanged Git blobs. This closes an **UC034 source** dependency only. M0 stays **UC037 read-only**; UC034 closure schema, attribution installation, FE14 pending-delivery staging, eligible current-cycle fixture, rollback and independent SQL reconciliation remain deferred behind later scoped approval.
