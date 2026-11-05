# UC028 Create V1 proposal — PROPOSED, not approved/applied

## Actor, scope và điều kiện

Housekeeping Supervisor theo SRS UC028; đề xuất gate bằng existing CLEANING_ASSIGN trên actual Room.BranchId, không role-name shortcut/numeric IDs, không CLEANING_CREATE mới. User/branch/access/session/account/employment active phải được revalidate dưới lock. Room/floor/area nếu có, room type branch/global applicability và active references phải hợp lệ. Cleaning có stayover nên không áp precondition Vacant; không phụ thuộc Workday hoặc UC138 full CRUD.

Room pointer + complete same-room task set phải consistent theo [Room contract](ROOM_LIFECYCLE_CONTRACT.md). Một task logical chưa COMPLETED/CANCELLED, hoặc workflow null/unknown → không tạo task cạnh tranh. Pending Inspection/Rework Required vẫn open. Trả existing authorized task identity nếu view policy cho phép; không leak ID cho caller chỉ có create nhưng thiếu view. Unknown legacy cycle→fail closed/explicit adoption riêng.

## Proposed HTTP/DTO (không có route này trong production)

- `GET /api/branches/{branchId:long}/cleaning-task-options?roomId={id}`: proposed read dưới CLEANING_ASSIGN, trả authorized room/type, active CLEANING_TYPE/PRIORITY refs và room-type item preview. Không lấy mock reference ID hoặc tự coi ROOM_VIEW đủ để tạo. New options API phải review routes/DTO/source permissions trước implementation.
- `POST /api/branches/{branchId:long}/cleaning-tasks`: proposed JSON `{roomId, cleaningTypeId, priorityId, dueAt, instruction}`. Required positive IDs, dueAt future UTC ISO; priority/type phải active đúng group (canonical seed có CLEANING_TYPE/PRIORITY, không hard-code numeric IDs). Instruction proposed optional≤4000 UTF16/no forbidden controls, cần duyệt exact validation. Không nhận actor/status/branch override/createdAt/start/end/items; DTO reject unknown members đề xuất cho endpoint mới.
- Task number do server cấp,≤40chars/unique theo existing task_no; proposed GUID-backed code, không COUNT+1. Response201 `{id,branchId,roomId,taskNumber,state,itemsInitialized:false}`, quoted uppercase HEX task rowversion ETag + Location tới authorized task read route sau khi route đó approved. Chưa xác định current source-read cho mọi create actor nên không giả rằng monitor luôn readable.

Không Create If-Match giả từ Room/UC032: room hiện không có rowversion; server serializes bằng retained Room lock+new current-pointer rowversion, conditional update bên trong transaction. Existing task rowversion được trả cho Assign. V1 Create không yêu cầu Notification Idempotency-Key vì không phải notification producer; lost response không auto retry. Duplicate Create conflict409/refetch authorized existing state, không replay201 giả. Nếu team yêu cầu replay cho Create, phải duyệt operation-ledger scope riêng, không mặc nhiên mở C01 mọi writes.

## Item snapshot — giữ tương thích UC032

Source hiện tại InitializeWorkProgressAsync(kind=cleaning) chỉ chạy sau task workflow/legacy IN_PROGRESS + ActualStartAt, copy room-type definitions vào cleaning_task_items và tạo progress revision. Vì vậy V1 **đề xuất preview tại Create, freeze tại first Start** để không đổi semantics UC032. Create yêu cầu current room type có1–100 approved cleaning definitions có thể preview; nếu không thì reject reference/configuration trước write. Không copy inspection criteria và không nhận client-defined cleaning items. Đổi template trước Start có thể đổi snapshot; quy tắc này phải được chủ dự án duyệt, UI phải nói preview. Khi Start, dùng reviewed feature helper cùng transaction, không gọi nested BatchAsync/HTTP initializer. Không tạo progress version giả IN_PROGRESS khi task mới Pending Assignment.

## Transaction / rollback

Một caller-owned Serializable HotelOpsDbContext transaction: sorted actor eligibility locks + session/permission; active Branch retained lock; feature gate/Room row + pointer range locks theo exact reviewed order; reread all prerequisites/open-task set/reference/templates; INSERT CleaningTask(PENDING_ASSIGNMENT/PENDING), advance current pointer, derive readiness with current unreleased blocks, INSERT readiness event và audit before/after/cause/actor/server-time; final session/grant/branch checks; one commit. No direct Room.OperationalStatus/occupancy/block writes, no inner commit.

Any validation, permission revoked after wait, duplicate, lineage/version mismatch, missing sidecar schema, event/audit failure or cancellation before commit → rollback all task/pointer/event/audit. Proposed statuses:400 invalid JSON/refs/time/CSRF,401 invalid session,403 permission/scope,404 authorized absent room,409 open task/state conflict,503 missing approved schema/dependency;201 only actual commit. Do not promise all provider failures map503; reconcile shared exception policy. No fresh SQL write acceptance authorized by FS007.

## Approval / acceptance

User: exact new DTO/routes, CLEANING_ASSIGN binding, preview/freeze timing, no direct legacy state write, sidecar history instead of legacy history, conservative unknown/legacy policy. DEV3/joint: exact sidecar DDL/EF/DI/room-source lock compatibility. DEV1: feature implementation after these contracts, no separate projects. Tests: valid Create, all refs/lineage, duplicates/concurrent requests, pending-inspection duplicate, unknown legacy, blocked/occupied room dimensions, final permission revoke, fault after task/before audit/before commit, persisted derived event + audit, unchanged legacy fields, UI refresh and independent UC032 regression. See [acceptance plan](LIVE_WRITE_ACCEPTANCE_PLAN.md).
