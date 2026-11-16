# UC055 — source completion, 2026-11-16

Status: IMPLEMENTED_PENDING_QA. This is source reconciliation against UC-055 and GBR-050/073/074/136/137/138, not COMPLETE_VERIFIED. The prior numeric-progress increment remains the implementation baseline.

| Requirement | Implemented source |
| --- | --- |
| Current assigned technician or Maintenance Lead, session, branch and permissions | MaintenanceResumeContract and MaintenanceExecutionAccess require current SQL session, MAINTENANCE_EXECUTE plus independent MAINTENANCE_VIEW, exact current assignee and corrective Incident relationship. |
| Display progress, diagnosis/actions, issued materials and attachments | MaintenanceReadView displays per-log percentage/notes and material lineage; read-only IncidentEvidencePanel independently authorizes attachments. MaintenanceProgressPanel loads eligible issued lines, supports units/quantities and now explicitly reports unavailable percentage tracking and prompts diagnosis/actions in the note. Historical absent percentages remain unknown. |
| Save percentage 0..100, notes, materials and audit atomically | CreateMaintenanceWorkLogRequest range validation; MaintenanceWorkService.Logs writes log, MaintenanceProgressStore, usages, audit and native-version increment inside the existing transaction. At 100 it neither closes the interval nor changes workflow status. |
| Reject material usage exceeding net issued quantity | MaintenanceWorkService.Materials proves Posted ISSUE, Incident/request/fulfillment lineage, approved snapshot and stock movement; inventory capacity locks and base-unit conversion guard consumption against returns and existing usages. Independent MR/inventory authority remains required. |
| Hold, reason, close interval; SLA continues | HoldWorkAsync requires current executor and IN_PROGRESS, records the required reason, sets maintenance ON_HOLD and closes the actual interval. Incident physical IN_PROGRESS is retained by the existing synchronized-pair mapping. No SLA timestamp/deadline is reset. |
| Resume only under workday/single-interval guards | ResumeWorkAsync and MaintenanceWorkService.WorkTime use the owning transaction and current workday/interval participant; source locks and native If-Match protect concurrency. |
| Materials Needed opens linked UC091/092 | Canonical Workspace provides Request materials for this incident, carrying Incident/branch/department into A004A-043. Material creation catalog and server admission independently validate the context; URL parameters grant no authority. |
| Save feedback and stale-response protection | Existing explicit review/confirm, CSRF, original ETag, session binding, no automatic retry, response identity/version checks and reload guidance. |

## Direct verification in this increment

- Backend solution build: PASS, zero warnings/errors. Solution-level test exits zero without discovery; not counted as tests.
- Focused progress-input, execution-access and material-access tests: 15 PASS.
- Frontend TypeScript/production build: PASS; existing React Router use-client and large bundle warnings remain.
- Progress browser suite: three checks PASS using mocked HTTP, not live HTTPS/SQL.
- Existing ModelProbe gained a focused `--maintenance-progress` mode: production EF store roundtrip, repeated installer, SQL range/duplicate/update rejection, enclosing rollback and synthetic-fixture cleanup. First build failed for a missing EF transaction extension import; corrected. Final ModelProbe build PASS with three pre-existing EF1002 warnings in SprintCredentialScenario.
- Both attempts to execute that probe timed out during SQL pre-login at the initial read-only identity check. Neither reached installation or mutation. SQL scenarios and rollback checks are NOT_RUN, not PASS. TLS verification was retained.

## Runtime dependencies / Human QA

Install reviewed native-version, progress and material capabilities only on verified disposable SQL; retain current grants, Working workday and actual intervals. Verify the full authenticated HTTPS progress -> Hold -> Resume path, 100% remaining open, quantity races/returns, permission revocation, stale ETags, atomic audit/rollback and attachment scope. Missing capability remains fail-closed for dependent writes. No shared database mutation, persistent installation or deployment occurred in this increment.

New source-complete UCs: 1 (UC055). New COMPLETE_VERIFIED UCs: 0. UC054 Decline/lead notification and UC139 remaining participant/runtime gaps remain separate and are not promoted.
