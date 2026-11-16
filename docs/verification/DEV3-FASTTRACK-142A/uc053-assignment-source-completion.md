# UC053 - Assign/reassign maintenance source completion

Status: IMPLEMENTED_PENDING_QA. NEW_SOURCE_COMPLETE=UC053; NEW_COMPLETE_VERIFIED=0.
Source coverage is 56/144; historical QA-verified count remains 27/144. This receipt does not claim the entire Triage -> Assign -> Start/Decline -> Reassign chain has passed runtime acceptance.

## Scope and implementation

SRS UC053 / GBR-071, 014, 007, 008, 010 and the Project Leader decision permitting a former assignee to read their own reassignment receipt.

- Incident detail offers assignment only after proven Triage and current Pending Assignment/Assigned state. The dedicated UI loads eligible branch technicians with scoped open Incident/occurrence counts, accepts instructions and planned end, and confirms the operation. SYS-PAR-10 display uses Asia/Ho_Chi_Minh; persistence receives UTC. Triage success now links to this admitted flow rather than the legacy demo action.
- GET /api/incidents/{id}/assignment returns current candidates and both Incident/work versions. POST to the same route uses current SQL session identity, HTTPS, global CSRF, current scoped Maintenance Lead MAINTENANCE_ASSIGN, independent Incident/maintenance read access, native Incident If-Match and work version. No client ActorUserId is accepted.
- Assignment requires current active branch Staff/Lead with execution/read grants. Every Lead assignee, including dual-role users, requires another current scoped Lead who can verify without historical performer conflict. No role, permission or grant is created automatically.
- Serializable transaction locks current actor/candidate, Incident and work; validates proven Triage and no execution; creates the initial PENDING work if needed or replaces the current assignee. Physical Incident OPEN and work PENDING remain unchanged. SLA deadlines and room blocks are preserved. Previous/new assignee, instructions, planned end and server time are audited atomically with immutable assignment receipt/notification intent. Native version checks and unique incident/version receipt reject replay; there is no automatic write retry.
- Current technician's scoped work population uses the new assignee. Committed intent delivery checks current recipient permissions, deduplicates event/recipient and retries within configured notification retention. The previous assignee receives a separate receipt target, not continued whole-work access.
- GET /api/maintenance-assignment-receipts/{id} and /maintenance-assignment-receipts/:id expose only an authorized former-assignee receipt. Current session, active role, MAINTENANCE_VIEW and branch are rechecked for delivery and source opening. Stored ActionUrl is not authority; Incident content is not disclosed.
- Versioned MaintenanceAssignment.sql is additive and identity guarded. No generated EF entity, existing physical status constraint, production grant or shared database was changed. Missing storage capabilities fail closed. Legacy assignment/create actions remain gated.

## Direct evidence in this run

- Backend build: PASS, 0 warnings / 0 errors. Initial missing SessionReference namespace errors were corrected before the successful build.
- dotnet test HotelOps.slnx --no-restore: exit 0 with no discovered test project; not counted as test coverage.
- Focused regression project: 20 PASS (MaintenanceAssignment, MaintenanceDecline, MaintenanceReview, IncidentReadAccess). Includes UTC/planned-end checks, current Lead grant/scope checks and former-assignee receipt boundaries. These do not prove SQL concurrency or runtime authorization.
- Frontend typecheck + Vite production build: PASS. Existing React Router ignored use-client directives and approximately 1,074 KB chunk warning remain.
- Browser, mocked HTTP: 4 PASS (2 assignment success/stale and 2 Triage). Checks explicit confirmation, CSRF, both versions, UTC+7 conversion, no client actor and no repeated write. An initial ambiguous Technician label selector caused two failures; explicit accessible label fixed it and all four passed on final rerun.
- SQL installer preview: PASS. Installer executed twice within an outer transaction against the identity-guarded disposable recovery database; rollback and original object/capability comparison PASS (UC053_INSTALL_TWICE_ROLLBACK_CLEANUP_PASS).
- SQL identity: meita\MSSQLSERVER01 / HotelOps_DEV3_142A_Recovery_2b221f40 / recovery GUID 5270D07C-7C27-477B-A60E-86485ADD7FAD. No persistent schema, grants, seed or business-data changes. Shared/production database untouched.
- Actual HTTPS API business execution, notification delivery and UI -> API -> SQL E2E: NOT_RUN.

## Human QA / activation

Install reviewed capabilities on the approved target and configure approved SLA targets without invented durations. Verify first assignment, reassignment, technician work list, Staff/Lead eligibility and independent reviewer, foreign branch/revoked grants, stale/replayed/concurrent requests, rollback, unchanged SLA/blocks, notification retry/dedup, and former-assignee receipt-only access. UC052 Cancel Duplicate/Invalid remains separate and incomplete.

JOB06: maintenance_assignment_receipts.audit_id is an additional immutable audit provenance dependency. Do not cascade, disable constraints or purge its referenced audit. Until a compatible reviewed archive and finite-retention/access policy exist, skip referenced records and record the blocker; notification retention is not receipt retention.

## Changed files

Backend: Controllers/MaintenanceAssignmentController.cs; Services/Maintenance/MaintenanceAssignment{Access,Eligibility,Store,Service,Service.Read}.cs; MaintenanceReviewAccess.cs; MaintenanceReviewSeparation.cs; MaintenanceWorkService.Commands.cs; Services/Notifications/MaintenanceAssignment{Source,Delivery}.cs; NotificationCenterService.cs; TaskNotificationWorker.cs; Services/Dev2Authorization/IncidentReadContract.cs; DTOs/Incidents/IncidentDetailDto.cs; Program.cs.

Frontend: dev2/MaintenanceAssignmentPanel.tsx; IncidentTriagePanel.tsx; MaintenanceReadView.tsx; Workspace.tsx; notifications/MaintenanceAssignmentReceipt.tsx; shared-host/SharedAuthHost.tsx. Tests: maintenance-assignment-browser.test.mjs; incident-triage-browser.test.mjs; MaintenanceAssignmentEligibilityTests.cs. SQL: scripts/verification/CanonicalDatabase/MaintenanceAssignment.sql. Documentation: this receipt and UC_STATUS_MATRIX.md.
