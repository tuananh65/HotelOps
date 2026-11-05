# Task 40 - DEV1 deactivation readiness

Reviewed on 2026-10-28 (Asia/Saigon), branch `feature/BE1-database-integration`, HEAD/upstream `6599692b578410ef1ea8642a7d1bf0827f32c195`. Index and tracked working tree were clean at preflight. All other worktrees and existing untracked artifacts are preserved. No Commit/Push/Merge or database connection is part of this task.

**DEV1 readiness: BLOCKED. Overall UC-006 Deactivate: BLOCKED. Task deliverable: PARTIAL readiness, source audit completed.** The implemented Activate endpoint remains separate; no Deactivate route or execution primitive is added. This is not a claim that DEV1 can deactivate safely while waiting only for DEV2.

Sources inspected: AGENTS.md; SRS draft UC-006, UC-029/030/041/046/047/104-106/115/119-121/138/139, its embedded Use Case table and transition appendix; SRS_CHANGE_NOTES; Task 35 approvals from this conversation; UserAccountStatus, UserAccountActivation and UserBranchAssignment records; current entities, DbContext, release schema, patches and application source. No separate Use Case Matrix file was found; the embedded SRS list at lines 990 onward is the available use-case mapping. The Permission Matrix remains draft; explicit UC-006 approvals govern this slice.

Both requested DEV2 files are **MISSING SOURCE in this workspace**:

- `DEV2_DEACTIVATION_FINAL_CONCLUSION.md`
- `DEV2_DEACTIVATION_RC_D1_UPDATE_FOR_DEV1.md`

Search included hidden/ignored files under this workspace, excluding .git, build outputs and nested worktree `obj`. No other worktree was read. The available `UserAccountActivation/DEV2_DEACTIVATION_HANDOFF.md` and `AccountActivationRelease/DEV2_HANDOFF_MESSAGE.md` are earlier DEV1 handoffs, not DEV2 sign-off. The Task 40 message's RC-D1 summary is used as the current cross-team reference and attributed as such. Missing documents are not represented as read.

Read `CURRENT_RESPONSIBILITY_MATRIX.md`, `WRITER_AND_LOCK_AUDIT.md`, `LAST_ADMIN_REVIEW.md`, `VERIFICATION_RESULTS.md` and `OPEN_BLOCKERS.md` for exact findings and closure gates. Old UserAccountStatus notes saying Activation HTTP is blocked were superseded by Tasks 36-37; they do not describe current Activate readiness.

## Exact change manifest

Only these six new documentation files are created in this task:

- `scripts/verification/Dev1Deactivation/README.md`
- `scripts/verification/Dev1Deactivation/CURRENT_RESPONSIBILITY_MATRIX.md`
- `scripts/verification/Dev1Deactivation/WRITER_AND_LOCK_AUDIT.md`
- `scripts/verification/Dev1Deactivation/LAST_ADMIN_REVIEW.md`
- `scripts/verification/Dev1Deactivation/VERIFICATION_RESULTS.md`
- `scripts/verification/Dev1Deactivation/OPEN_BLOCKERS.md`

No C# helper is added: there are no implemented DEV1 responsibility writers to retrofit, lifecycle/eligibility predicates conflict or remain undefined, and a helper without integrated callers would not close the race. Existing SQL user locks are a reusable mechanism, not evidence of writer coordination or last-admin serialization. No schema, generated entity, RBAC Catalog, DEV2 source or shared infrastructure is changed.
