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

## FE05 effective readiness (approved after checkpoint31)

Keep existing rooms.operational_status values. Never write BLOCKED into that column or translate it to OUT_OF_SERVICE. Calculate effective readiness using GBR-044: inactive first, then any effective block, then current cleaning/inspection lifecycle. Passed inspection is Ready only without effective blocks and with all lifecycle conditions met. Removing one block must recalculate from current data, including other blocks and the current cycle.

APIs, Room Status Board, Dashboard and all room-usability consumers must use effective readiness. Unsupported or unknown readiness must fail closed for use decisions. Preserve history, transaction, concurrency and MSG17. Verify Passed+block, multiple blocks, removal, failed inspection and concurrent block changes. Do not alter generated entities or CHECK constraints to introduce status codes.

Checkpoint32 actual schema finding: UX_room_blocks_active uniquely constrains room_id for unreleased rows, conflicting with multiple incident-room blocks. A narrow guarded disposable-only trial replaces it with uniqueness on (room_id,incident_id), matching GBR-075. The trial is rolled back together with synthetic fixtures; no lasting schema deployment has occurred. Existing room_status_history CHECK values and room_readiness_events' PENDING_CLEANING-only CHECK are retained; effective transition history/writer composition is still pending.
