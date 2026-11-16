# UC139 — Unscheduled Start source

UC status remains PARTIAL_IMPLEMENTATION / NOT_TESTED. Newly completed UCs: 0. This increment implements the approved Unscheduled Start subflow; full UC139 still needs non-Task Pause All adapters and runtime acceptance.

## Implemented

The existing authenticated Start endpoint accepts a reason and optional department only when no shift assignment is supplied. Active employee, current session, branch access and ATTENDANCE_RECORD remain mandatory. Membership comes from a dedicated employee_department_memberships table, never User.DepartmentId or management designation. The server selects a sole valid membership or requires a choice among multiple current memberships. Active branch and department are checked. The existing user/time serialization and one-Working-workday constraint remain authoritative.

Workday, reason, audit and durable notification intent are written in one transaction. The existing TaskNotifications worker retries pending intents in an independent transaction. Missing eligible recipients are recorded as NO_RECIPIENT. The official department designation identifies the candidate Head; explicit management mapping, current DEPT_MANAGER, active account/employment, branch access and scoped WORKLOAD_VIEW independently authorize delivery. No role/grant is inferred or created. Recipient authorization is checked again before delivery commit and source resolution. Failure rolls back notification delivery only, never the committed Workday. Workday discovery errors do not block unrelated task notification delivery.

Two scoped GET endpoints provide verified own departments and the authorized Head's unscheduled-workday summary. The summary uses the same Head access rule and final session/permission validation. UI includes reason, server-verified department selection, review and CSRF-protected submission, identity binding, explicit response/department validation and no automatic retry after uncertain outcomes. Notifications resolve through an allowlisted local Workday URL to the protected summary.

The development SQL migration adds explicit membership and notification-intent storage plus WORKLOAD_VIEW catalog metadata, with exact disposable identity guard, version marker and trusted FKs. It seeds no membership or grant. Existing generated entities and Workday schema are unchanged. Workday reason uses existing nvarchar(1000); notification intent contains identifiers/state only, not a second copy of the reason. Delivery honors the existing notification retention window; no new indefinite personal-data retention policy is introduced.

## Direct evidence (2026-11-16)

- Backend solution build: PASS, zero warnings/errors.
- Frontend TypeScript + Vite: PASS. Existing React Router directive / large bundle warnings remain.
- Ten focused regression tests: PASS (controller admission and membership selection/denial).
- 78 transport tests: PASS, including explicit unscheduled request, required reason, returned-department mismatch, scheduled regression and Pause All.
- Three focused browser tests with mocked API: PASS (scheduled Start/End, Task Pause All, unscheduled Start with verified membership/reason).
- Workday read-routing verifier: 64 PASS after correcting its obsolete single-route-parameter assumption for summary's workdayId. Scheduled foundation verifier: 62 PASS. Source checks are not SQL behavior proof.
- dotnet test HotelOps.slnx --no-restore: exit 0 without test discovery; not counted as test coverage.
- Read-only SQL identity and catalog verified on the approved recovery disposable database. WORKLOAD_VIEW was absent.
- Migration preview PASS with rollback. Two installs inside an enclosing transaction PASS: one catalog entry, six trusted FKs, no membership/grant seed, complete rollback to the verified initial schema/catalog. No changes were left installed; no shared/production mutation.

## Runtime dependencies and remaining acceptance

Deployment must install the guarded migration, provision approved real employee memberships and scoped WORKLOAD_VIEW grants, and enable the existing TaskNotifications worker. None of those grants/memberships or configuration changes was applied in this increment. A missing schema remains fail-closed; an unavailable Head retains a retryable intent.

Actual SQL execution of the C# Start/Head read/delivery paths, authorized/revoked/cross-branch cases, races/rollback and UI -> HTTPS -> SQL are NOT_RUN. Migration execution proves only DDL/idempotency/cleanup. Human QA must verify those business cases before accepting this subflow. UC139 also requires Cleaning/Rework/Incident Pause All adapters, preserving each owning lifecycle rather than forcing Operational Task state onto them.
