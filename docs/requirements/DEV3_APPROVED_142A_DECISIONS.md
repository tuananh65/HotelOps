# DEV3 approved FASTTRACK-142A decisions

Recorded 2026-11-15 from explicit user approval in the active sprint. These decisions override conflicting historical drafts/catalog grants. They do not authorize shared HotelOpsDB or production mutations.

## UC110: TASK_PAUSE_REASON

Approved initially active codes: WAITING_MATERIAL, WAITING_EQUIPMENT, WAITING_APPROVAL, ACCESS_BLOCKED, SAFETY_ISSUE, OTHER. Reason is mandatory. Note is optional for the first five, required and non-whitespace for OTHER; enforce actual persistence length. Resume requires no new reason.

Use reference_values group_code/value_code uniqueness, never fixed IDs. Server validates group/code/active/note. Preserve historical meanings and existing deactivation. Seed idempotently only in verified disposable SQL. Only the current authorized executor may Pause In Progress or Resume Paused. Preserve ETag, responsibility, workday, branch scope and existing state/version authority. State, interval, history and audit commit atomically; effort excludes paused time.

Checkpoint31 implementation: additive task_pause_events references existing progress/reference/interval rows and snapshots code/label/note (nvarchar(1000)). It does not repurpose BlockerReason or edit generated EF entities. The seed/schema script has exact instance/database/GUID/run guards; it is not a production migration.

## UC135: FE15 report authorization

Operations Manager or Owner AND REPORT_VIEW, with applicable branch/department scope. DEPT_MANAGER and all other roles are denied even with REPORT_VIEW, unless an allowed role supplies appropriate access. Enforce the same feature policy in API, UI, query and CSV export using existing role mapping/principal; no hard-coded role IDs and no global revocation of REPORT_VIEW.

UC136/137 keep their separate Administrator actor; do not apply UC135 policy to them. Preserve CSRF, snapshot consistency, CSV safety and audit. Add positive/negative tests, especially DEPT_MANAGER with REPORT_VIEW.

Checkpoint31 status: decision is approved, no further role approval needed. Feature policy, report admission/API/UI/export audit implementation remains PENDING; this checkpoint does not claim it is applied.

## Remaining contracts

FE05 Room Blocked mapping and FE13 read authorization are not resolved by these decisions. Resolve against SRS and actual schema or state the exact conflict; never bypass protection. Database edits must remain narrow, additive and isolated.
