# Task74 — independent progress contract proposal

Status: **CONDITIONALLY APPROVED** by explicit developer reply in the Task74 session, 2026-10-31; implemented and verified only on disposable trials. Priority: user instructions, AGENTS, approved requirements, Change Notes, SRS. Không thay đổi DEV2 source, UC006 gates hoặc production/shared DB.

## Selection and blockers

| UC | Exact title / weight | Required contract |
| --- | --- | --- |
| UC032 | Update Cleaning Progress / 2 | Distinct cleaning items and per-task checked state, progress notes/private photos, active current assignee, IN_PROGRESS only, strong ETag, atomic audit. Không dùng inspection criteria làm cleaning items. |
| UC109 | Update Task Progress / 2 | Explicit completion criteria/results, notes/private evidence, current assignee, IN_PROGRESS/PAUSED only, strong ETag, atomic audit. Không suy PAUSED từ legacy BLOCKED. |
| UC028 | Create Cleaning Task / 3 | Pending Assignment, room-type cleaning items, one open cycle, room status/history recalculation with effective-block precedence; phụ thuộc shared room lifecycle/writer protocol. |
| UC012 | Update Branch Information / 2 | Create/read/update và guarded deactivate; deactivate cần no active rooms/stock/open tasks/incidents/requests và compatible DEV2 producers. Chưa thể complete nếu chưa có writer proof. |
| UC026 | Deactivate Department / 5 | Active members/open assignments/reference guards, compatible DEV2 writers; cần kiểm tra dependency contract trước chọn implementation. |

UC037/044/045/047/107/138 không phải safe replacement để đạt full credit khi notification delivery/retry chưa có. UC016/029/041/046 và UC006 bị loại theo task. Không hạ SRS để tính full UC.

## Proposed additive V1 scope requiring approval

1. Disposable-only SQL scripts (không EF migration): riêng room-type cleaning item definitions + immutable per-cleaning-task snapshots/check state; operational task criterion definitions/results. Không bulk convert legacy, không tái sử dụng inspection items. FK ownership cùng task/type/branch, uniqueness và bounded text/results validation. Exact DDL sẽ được review theo những invariant này.
2. Nullable opt-in workflow metadata/rowversion cho operational task progress nếu cần biểu diễn PAUSED chính xác; preserve legacy coarse status và fail closed khi workflow unknown. Existing housekeeping rowversion tái sử dụng cho cleaning. Không tạo Workday/WorkInterval, không invent start/resume workflows hoặc attendance equivalence.
3. Relational evidence links riêng cho progress, reuse private attachment validation/storage/download authorization; không dùng audit JSON làm business criterion storage. Không sửa generated entities: controlled unmapped SQL projections/partial mapping chỉ nếu cần và được review compatibility.
4. Reuse existing SQL CLEANING_EXECUTE/TASK_EXECUTE grants; business ownership là current active SQL assignment; không tạo quyền/grant. Existing BatchAsync branch permission không tự đáp ứng OWN: cần feature-specific SQL OWN evaluation với eligible grants và current assignment. Giữ initial/final session/permission gates, sorted participant User locks, retained parent/task locks và audit transaction.
5. No state transition/time event: progress 100% giữ trạng thái hiện tại; save criteria/notes/photos/progress và audit atomic; stale If-Match reject. File cleanup khi SQL rollback/cancel. Fresh/upgrade/idempotence/rollback và real HTTPS/SQL acceptance chỉ trial.
6. UC028 room status/block và UC012/026 deactivation chưa được phê duyệt qua proposal này. Giữ PARTIAL/BLOCKED nếu thiếu coordination/concurrency acceptance. Không sửa DEV2 writers để vượt blocker.

Developer approved additive cleaning items/task criteria/evidence/concurrency schema, constraints/indexes, legacy-compatible Database First mapping, initial/final SQL authorization, atomic audit, fresh/upgrade/replay/rollback/recovery and real HTTPS/SQL acceptance. No new permissions/grants, DEV2-owned changes, bulk legacy conversion, shared/production deployment or Git mutations authorized. Mandatory criteria must not be bypassed on completion; notification/room-writer dependencies must remain partial if applicable.

Implementation choices under this approval: distinct immutable per-task cleaning snapshots and task criteria; opt-in progress revision tables (not Workday/WorkInterval); stored PAUSED is explicit and never inferred from BLOCKED; existing attachment types CLEANING_TASK/TASK with typed relational source links; four controlled DEV1 entity mappings disable bare SQL OUTPUT for optional triggers. Legacy parent/assignment updates advance revision; opt-in terminal completion is SQL-rejected if required item/result is missing. No new complete/start/resume workflow is introduced.

UC028 room lifecycle and UC012/026 dependency-writer coordination remain outside this approval. Tất cả UC còn mandatory gap được 0 verified points. Final evidence/classification: [Task74 implementation](TASK74_BATCH5_IMPLEMENTATION.md).
