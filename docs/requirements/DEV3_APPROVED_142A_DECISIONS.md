# DEV3 approved FASTTRACK-142A decisions

Recorded 2026-11-15 from explicit user approval in the active sprint. These decisions override conflicting historical drafts/catalog grants. They do not authorize shared HotelOpsDB or production mutations.

## UC110: TASK_PAUSE_REASON

Approved initially active codes: WAITING_MATERIAL, WAITING_EQUIPMENT, WAITING_APPROVAL, ACCESS_BLOCKED, SAFETY_ISSUE, OTHER. Reason is mandatory. Note is optional for the first five, required and non-whitespace for OTHER; enforce actual persistence length. Resume requires no new reason.

Use reference_values group_code/value_code uniqueness, never fixed IDs. Server validates group/code/active/note. Preserve historical meanings and existing deactivation. Seed idempotently only in verified disposable SQL. Only the current authorized executor may Pause In Progress or Resume Paused. Preserve ETag, responsibility, workday, branch scope and existing state/version authority. State, interval, history and audit commit atomically; effort excludes paused time.

Checkpoint31 implementation: additive task_pause_events references existing progress/reference/interval rows and snapshots code/label/note (nvarchar(1000)). It does not repurpose BlockerReason or edit generated EF entities. The seed/schema script has exact instance/database/GUID/run guards; it is not a production migration.

## UC135: FE15 report authorization

Operations Manager or Owner AND REPORT_VIEW, with applicable branch/department scope. DEPT_MANAGER and all other roles are denied even with REPORT_VIEW, unless an allowed role supplies appropriate access. Enforce the same feature policy in API, UI, query and CSV export using existing role mapping/principal; no hard-coded role IDs and no global revocation of REPORT_VIEW.

UC136/137 keep their separate Administrator actor; do not apply UC135 policy to them. Preserve CSRF, snapshot consistency, CSV safety and audit. Add positive/negative tests, especially DEPT_MANAGER with REPORT_VIEW.

Checkpoint32: feature policy and preview/CSV API admission/audit are implemented and HTTPS/SQL tested. Canonical report UI remains pending; no full UC135 acceptance.

## Remaining contracts

FE13 read authorization remains unresolved. Database edits must remain narrow and isolated.

## Autonomous execution directive (checkpoint39)

DEV3 may decide routine technical/SRS-defined matters and integrate directly on the canonical branch. Keep the original 2026-11-16 17:50 UTC deadline. Use UC v3/SRS, approved decisions and actual contracts; document narrow decisions, preserve security, and continue independent work where a dependency remains fail-closed. No shared database or production mutation, force-push or main/develop merge. No routine approval questions.

Decision39: UC035 initial assigned-source read is independent of Start/workday and does not open an interval. Reuse actual committed authority, session, ownership and CLEANING_VIEW; do not lower proof DML-denial checks. Canonical notification/rework login returns use the existing safe local destination registry. UC046 completion is assessed at its explicit Pending Acceptance handoff, separately from UC035/047. UC109 completion is assessed for an already assigned In Progress/Paused task, separately from creation/completion UCs. Exact acceptance evidence and remaining runtime limits are in CHECKPOINT-39.md.

## FE05 effective readiness (approved after checkpoint31)

### DEV3 technical decision41: isolated restricted SQL runtime

Use SQL Server application-role activation for the disposable Development HTTPS trial. The existing principal proof-DML denial remains mandatory; there is no special successful path for an administrator. Activation uses direct RPC, no reversal cookie, encrypted nonpooled connections, and no ambient transaction enlistment. Verify actual server/database GUID and ownership marker before activation; verify the effective role and denied database CONTROL afterwards. Invalid identity/secret/configuration closes the connection. Every new EF connection and the raw startup schema check must activate the role before application queries. No server login, server setting, shared database, generated entity or production configuration change is authorized by this trial.

Temporary roles receive SELECT/metadata and only the authentication/rework/audit/interval writes needed for the selected workflow, with explicit proof DML denial. Passwords are random and passed only in process memory/environment. The verifier owns host shutdown, role removal, synthetic-row and private-file cleanup. This is an isolated acceptance mechanism, not a production deployment plan. SQL behavior was checked against [Microsoft application-role documentation](https://learn.microsoft.com/sql/relational-databases/security/authentication-access/application-roles) and [connection pooling guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling).

Keep existing rooms.operational_status values. Never write BLOCKED into that column or translate it to OUT_OF_SERVICE. Calculate effective readiness using GBR-044: inactive first, then any effective block, then current cleaning/inspection lifecycle. Passed inspection is Ready only without effective blocks and with all lifecycle conditions met. Removing one block must recalculate from current data, including other blocks and the current cycle.

APIs, Room Status Board, Dashboard and all room-usability consumers must use effective readiness. Unsupported or unknown readiness must fail closed for use decisions. Preserve history, transaction, concurrency and MSG17. Verify Passed+block, multiple blocks, removal, failed inspection and concurrent block changes. Do not alter generated entities or CHECK constraints to introduce status codes.

Checkpoint32 actual schema finding: UX_room_blocks_active uniquely constrains room_id for unreleased rows, conflicting with multiple incident-room blocks. A narrow guarded disposable-only trial replaces it with uniqueness on (room_id,incident_id), matching GBR-075. The trial is rolled back together with synthetic fixtures; no lasting schema deployment has occurred. Existing room_status_history CHECK values and room_readiness_events' PENDING_CLEANING-only CHECK are retained; effective transition history/writer composition is still pending.

## UC045 rejection recipients (DEV3 approved, checkpoint35)

On Failed inspection, notify every active Housekeeping Supervisor with valid access to the inspection branch and INSPECTION_VIEW, plus the actual cleaner identified by official cleaner attribution. Deduplicate to one notification per user/event. Recheck recipient eligibility before delivery and source authorization when opening the record. Persist notification intent in the Reject transaction; rollback must publish nothing. Preserve retry, idempotency and audit, without expanding inspection read permissions.

UC045_SUPERVISOR_RECIPIENT_SCOPE=ALL_ELIGIBLE_SUPERVISORS_IN_INSPECTION_BRANCH. No shared database mutation or production deployment is authorized.

## UC046: Request Rework assignee (approved during checkpoint37)

Apply GBR-046: active account/employment, valid active HK_STAFF assignment, current branch access and matching active Housekeeping department. Request Rework and its options do not require a currently active PLANNED shift. Preserve separate Accept/Start/Resume shift and workday guards. Require current Failed inspection, no open rework, selected failed defects, assignee and future UTC deadline; create Pending Acceptance plus audit atomically. Assignee notification must follow commit and deduplicate. No shared database or production changes.
