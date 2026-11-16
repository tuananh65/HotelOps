# UC139 / UC055 Maintenance Pause All and Resume

Source increment; UC139 and UC055 remain PARTIAL_IMPLEMENTATION. Newly COMPLETE_VERIFIED UCs: 0.

## Implemented behavior

Workday Pause All dispatches corrective MAINTENANCE intervals to an internal owning-workflow adapter. It requires a current MAINTENANCE_STAFF execution grant and independent MAINTENANCE_VIEW, current assignee, branch access and coherent Incident/location source. It locks Incident then Maintenance, checks the exact open interval, closes it, sets the existing maintenance status ON_HOLD, and writes the End of Shift work log plus audit with END_OF_SHIFT, interval/workday IDs and server time. These writes and Workday End share the existing serializable session/ETag transaction. No deadline, SLA, assignment, completion or Incident physical status is rewritten. Any remaining IN_PROGRESS Maintenance (including legacy work without an interval) prevents End and rolls back the whole Pause All transaction.

The new production contract admits only MaintenanceWorks.ResumeWork (A004A-028). It requires HTTPS, current unrestricted SQL session, active assigned MAINTENANCE_STAFF, same-role MAINTENANCE_EXECUTE, independent MAINTENANCE_VIEW, current branch access and coherent corrective source. The resource resolver supports existing OWN/BRANCH grants without broadening them. Exact controller/action/root/request binding remains mandatory; client ActorUserId must match the independently validated principal and never supplies authority. All other legacy Maintenance mutations keep their existing admission gates.

Resume compares the current native rowversion with If-Match after source locks, requires ON_HOLD, preserves historical ActualStartAt, and opens a new interval under the existing Working workday/single-open-interval guards. State, interval and audit commit together. Duplicate/stale requests cannot create a second interval; the client must reload after uncertain outcomes. The returned detail is scoped and excludes material references/usages, which require independent read permissions. Capability disclosure is rechecked before returning the read response.

Canonical operations navigation now includes the existing scoped Maintenance list/detail and the versioned Resume panel. Resume is not exposed through the generic unversioned action form. The panel requires server canResume + a strong execution ETag, asks for confirmation, obtains current CSRF, binds the session, sends If-Match and disables retries until reload. Workday UI recognizes Maintenance Pause All. Read-only workspace mode suppresses Resume.

## Schema and safety

MaintenanceExecutionVersion.sql adds only maintenance_works.row_version (SQL native rowversion) plus a version marker. No EF generated entity/mapping, CHECK status, grant, role or database data is replaced. The installer requires exact disposable server/database/nonzero GUID, runs with a migration lock, and supports rollback. Pause All and Resume fail closed without the verified concurrency capability; Pause All also requires the compiled production Resume contract so it cannot strand work behind a demo-only Resume.

No persistent installation was performed. No shared/production database mutation occurred. Existing work logs and audit retention apply; this increment does not introduce another authoritative workflow state or claim immutable audit storage.

## Direct verification, 2026-11-16

- Backend solution build PASS, zero warnings/errors.
- Frontend TypeScript/Vite build PASS. Existing React Router module-directive and >500 kB bundle warnings remain.
- Focused backend tests: 16 PASS, covering role/grant/branch/revocation boundaries, exact Resume invocation binding and existing Maintenance read/Workday admission regression. An initial GLOBAL-scope fixture expectation and a test actor-constructor compile error were corrected; the final run passed.
- Three mocked-browser checks PASS: Maintenance Pause All confirmation and unknown-source denial for scheduled/unscheduled workdays. These do not execute SQL.
- dotnet test HotelOps.slnx --no-restore exits 0 without test discovery; not counted as coverage.
- Verified disposable SQL: two native-rowversion installations in an enclosing rollback transaction PASS; type/length verified and original schema/marker absence restored. Local script: artifacts/uc139-maintenance-resume/migration-rollback.sql (ignored artifact).
- Auth/router browser regression: 49 PASS with mocked HTTP. The existing esbuild import.meta/iife test-bundle warning remains. This suite proves auth-routing regression, not the new Resume UI or live authorization.
- Actual UI -> HTTPS -> SQL Pause/Resume, revocation/concurrency races and rollback: NOT_RUN. Full UC acceptance is not claimed.

## Remaining work

Install the guarded migration only in the authorized runtime environment, then verify Pause All -> End -> next Working workday -> Resume with real SQL session/CSRF and rollback/negative cases. Complete canonical Start/Submit/Return-re-execution and legacy interval reconciliation; preventive occurrence integration remains separate. UC139 cannot be promoted to COMPLETE_VERIFIED from builds, mocked browser tests or DDL checks alone.

## Manual Hold increment, 2026-11-16

The production contract now also admits HoldWork (A004A-027), with exact action/request binding and the same current session, execution/read grants, assignee and branch checks as Resume. Both mutations require native rowversion If-Match under source locks and return scoped detail without material disclosure. Hold requires the existing minimum ten-character trimmed reason, closes the work interval and updates state/audit in the owning transaction. Existing SLA and Incident physical status are preserved.

Canonical detail now exposes Hold only when the server confirms execution eligibility and an owned open interval. The existing panel requires a reason and confirmation, obtains current CSRF and sends the original ETag once. Hold and Resume are excluded from the generic unversioned action form; their workspace capabilities remain independent.

Direct verification for this increment: backend build PASS (zero warnings/errors); frontend TypeScript/production build PASS (existing React Router directive and bundle-size warnings); 11 focused backend tests PASS; two Hold/Resume mocked-browser tests PASS. The browser fixture initially omitted the session CSRF field and failed admission; the fixture was corrected without weakening the production decoder. Solution-level dotnet test exited zero without test discovery and is not counted as coverage. No SQL mutation or live HTTPS/API verification was performed in this increment.

UC055 still requires the remaining progress/material workflow and acceptance verification; neither UC055 nor UC139 is promoted. The approved Rework ON_HOLD / physical IN_PROGRESS pair is already represented by ReworkEndOfShift.sql and the owning Hold/Resume services; this increment does not change that contract.

## Assigned Staff Start increment, 2026-11-16

The execution contract additionally admits A004A-026 StartWork for the current assigned Maintenance Staff with independent current execution/read grants. Start now requires the native If-Match under source locks and returns scoped detail with the updated execution ETag. The existing transaction changes PENDING work / OPEN incident to IN_PROGRESS, opens the real Maintenance interval with Working-workday and single-interval checks, and records actual start plus audit. Pending work with historical ActualStartAt is rejected rather than overwriting evidence. No schema, generated entity, role or grant is changed.

Canonical detail has a separately gated Start capability and uses the existing confirmation/CSRF/session-bound panel. The generic unversioned form does not expose Start. Existing Hold/Resume behavior is retained.

Direct checks: backend build PASS with zero warnings/errors; TypeScript/production frontend build PASS with the existing directive and bundle warnings; 12 focused backend tests PASS; three mocked-browser checks (Start, Hold, Resume) PASS. Solution-level test command exits zero without discovery. Actual SQL/HTTPS Start -> Hold -> Resume remains NOT_RUN; no database mutation performed. UC054 remains partial: Decline/lead notification, Maintenance Lead execution scope, complete evidence/deadline presentation and end-to-end acceptance are not covered by this increment. No newly completed UC is claimed.

## UC054/055 evidence and numeric progress increment (2026-11-16)

Maintenance detail now reuses the Incident evidence reader before execution and while reviewing progress. Its independent Incident API still enforces current evidence permission/scope. This embedded view is read-only even if the API reports canAdd, and is suppressed when the authorized submission evidence picker is already rendered. No grant or target permission is inferred from MAINTENANCE_VIEW.

GBR-050 numeric progress was missing from the Maintenance log DTO/storage. Added nullable ProgressPercent with server range 0..100, a guarded per-log maintenance_log_progress store, scoped detail history and capability-aware UI. Percentage is recorded with its owning log, quantities, audit and version update in the existing transaction. At 100, status and interval remain unchanged; Submit/Verify are still explicit actions. Historical logs without percentages stay unknown, never inferred as zero. Existing note-only clients remain valid. The form requires an integer percentage when storage is available and checks the echoed value after saving.

MaintenanceProgress.sql creates only a two-column table keyed/FK-linked to maintenance_work_logs, a 0..100 CHECK, immutable-update trigger and version marker. It does not alter generated entities or add an alternate lifecycle state. Exact disposable server/database/nonzero GUID, migration lock, incompatible-schema rejection and ApplyChanges=0 rollback are required. The installer was previewed successfully on the verified Recovery disposable. A subsequent cleanup read initially hit login timeout; retry verified identity and absence of the table/marker. No persistent migration or shared database mutation.

Backend solution build PASS, zero warnings/errors. Nineteen focused backend input/execution/read tests PASS; solution test exits zero without discovery. Frontend TypeScript/Vite build PASS with existing use-client and large-bundle warnings. Four mocked browser evidence/submission tests PASS, plus three progress/material/stale-version tests now exercising percentage bounds and 100% without closing work. An initial test-file append used the wrong working directory and failed without changing source; corrected before the successful runs. These are not live HTTP/SQL progress-write or full rollback/concurrency tests. New store insertion/read and immutable trigger DML still require runtime QA.

UC054/055 remain PARTIAL in the matrix pending final business source reconciliation and live acceptance; no COMPLETE_VERIFIED increment. UC054 Decline and lead notification remain missing. Next: complete Decline with explicit Pending Assignment mapping, then verify full Maintenance lifecycle with required schema and current grants.

Changed files: frontend IncidentEvidencePanel, MaintenanceReadView, MaintenanceProgressPanel and two browser suites; CreateMaintenanceWorkLogRequest, MaintenanceWorkLogDto, MaintenanceWorkDetailDto, MaintenanceReadContract, MaintenanceWorkService.Logs; new MaintenanceProgressStore, MaintenanceProgress.sql and MaintenanceProgressInputTests; UC_STATUS_MATRIX and this receipt.
