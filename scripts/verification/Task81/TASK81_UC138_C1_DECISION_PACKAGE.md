# Task81 — UC138 C1 decision package

**READINESS ONLY. C1 chưa được duyệt; không có writer/API/schema/EF/DI/RBAC production mới.**
Root D:\SEP490\Projects\HotelOps; branch feature/BE1-database-integration;
HEAD4a187320643d0c82f232c6b9dd5a667b8401c280; upstream origin/feature/BE1-database-integration.
Task80 artifact hashes kiểm tra trước build:0 mismatch. Task77–80 là baseline lịch sử,
không cộng lại thành kiểm thử mới. UC138 NOT_IMPLEMENTED, UC139 BLOCKED_EXTERNAL;
26/75 UC,60/256 điểm=23.4375%, không cập nhật ledger cũ.

## 1. Evidence và hiện trạng schema

Đã đọc AGENTS, toàn bộ báo cáo/approval/validation Task80, UC138 và màn hình Shift Management
trong SRS, SRS_CHANGE_NOTES, model/mapping ShiftTemplate/WorkShift/ShiftAssignment/Workday,
WorkTime helpers/participant/B1.1, SqlSessionPolicy, PermissionService/data source và runner.
SRS gốc vẫn là historical draft; giữ các clarification DH quản lý cả branch, department là
ngữ cảnh nghiệp vụ. Không suy ra branch DH từ Department.ManagerUserId.

Nguồn chính: SRS §12.2 UC138 (lines11650–11730), screen Shift Management (15160–15210),
GBR003/004/005/007/008/010/017/141; Entities/*.cs và Data/HotelOpsDbContext.cs.
Metadata SQL thật, EF inspection, keys/FKs/indexes/grants lưu trong sql-readiness-receipt.json.

| Hạng mục | Source/SQL thực tế | Phân loại / tác động |
|---|---|---|
| ShiftTemplate | PK shift_template_id; FK branch; code varchar30/name nvarchar100; time start/end; BreakMinutes, CrossesMidnight, IsActive; unique(branch_id,shift_code) | Đã có SQL+EF. Case-insensitive GBR003 phải kiểm tra/chuẩn hóa; không coi unique index tự giải quyết mọi collation |
| WorkShift | PK shift_id; FK branch,department,template,creator; ShiftDate, ScheduledStartAt/EndAt, RequiredStaffCount; default PLANNED | Đã có SQL+EF. Concrete UTC interval dùng cho conflict/UC139; không đồng nhất với template wall times |
| ShiftAssignment | PK shift_assignment_id; FK shift,user,assigner; ASSIGNED default, ConfirmedAt,CancellationReason; unique(shift_id,user_id) | Đã có SQL+EF. Unique chỉ chặn cùng pair, không chặn hai shift khác chồng lấn |
| Rowversion3 bảng | SQL metadata xác nhận0 row_version trên cả3; EF không có concurrency token cả3 | Chưa có, không phải có SQL nhưng thiếu mapping. GBR005 lost-update acceptance chưa thể đạt |
| Workday/Interval | A1 SQL+EF rowversion, FKs, one WORKING/user, one OPEN/user; Workday FK shift_assignment | Đã có, giữ nguyên. Không tái sử dụng version Workday cho Shift; không dùng ShiftAttendance thay interval |
| Cancelled/Active | Template IsActive thật; AssignmentStatus varchar mặc định ASSIGNED, helper chỉ loại explicit CANCELLED | CANCELLED là flow SRS/fixture, chưa có writer production; không hard delete. Unknown legacy status không mặc nhiên là rảnh |
| Overnight | Template có CrossesMidnight; concrete WorkShift timestamp có thể qua ngày; helper kiểm tra instant range | Đã có field/math; template materialization và equal-times semantics PENDING_DECISION |
| Transaction/audit | UserEligibilityLock và A1 caller-owned SQL tx; B1.1 Serializable/session/permission gates; AuditLogs hiện hữu | Nền tảng đã có; chưa có UC138 writer/audit/outbox transaction để chứng nhận |
| Notification | Entity Notification/Recipient hiện hữu; checkpoint DEV2 draft-only, publisher/outbox/replay/worker chưa implement | BLOCKED, không xem entity/outbox enqueue là delivery |

Không có API quản trị UC138 hiện tại. GET own-shifts của B1.1 chỉ trả ca đủ điều kiện của
actor, không thay weekly roster/admin permission. Source writer UC029/041/046 vẫn chỉ ở
cf8fb02; Task81 không restore/cherry-pick và không lặp lại discovery đó.

## 2. Proposed API/DTO contract — tất cả chưa active

Prefix đề xuất `/api/branches/{branchId}/shifts`. Bên dưới là contract review, không phải route
đã triển khai. Actor chỉ từ ICurrentUserAccessor. UUID OperationId cho mutations cần durable
replay owner; exact header/body placement PENDING_DECISION, không tạo store tạm.

Response records: ShiftDefinitionDto{id,branchId,code,name,startTime,endTime,crossesMidnight,
isActive,rowVersion}; ShiftAssignmentDto{id,shiftId,templateId,employeeId,departmentId,date,
scheduledStartUtc,scheduledEndUtc,status,rowVersion}; RosterDto{from,to,departmentId,rows}.
RowVersion là actual SQL binary8, ETag strong quoted base64; không audit-hash substitute.
DTO không lộ EF entity, password/session hoặc thông tin payroll.

| Operation / đề xuất endpoint | Request / validation cụ thể | Permission, version, tx/audit / tests đặc thù |
|---|---|---|
| Create definition POST `/definitions` | {code,name,startTime,endTime,crossesMidnight,operationId}; trim bắt buộc, schema lengths30/100, active branch, case-insensitive duplicate; overnight/equal-times rule PENDING_DECISION | SHIFT_MANAGE trên authoritative branch context, không If-Match cho insert. Sorted actor locks, unique SQL conflict409, CREATE audit+durable event/intents nếu affects users. Test duplicate case, inactive branch, invalid times, payload replay, rollback |
| Edit definition PUT `/definitions/{id}` | Same editable fields + reason/operationId; ID->branch resolver; BreakMinutes legacy được giữ, không tự reset; propagation policy PENDING_DECISION | SHIFT_MANAGE; If-Match actual template version; UPDATE WHERE id/branch/version, affected1. Lock template và all affected employee/source/version sets nếu propagate. Tests stale412, propagation/overlap, historical preservation, concurrent edit |
| List definitions GET `/definitions` | filters active/department context, pagination; positive references, department in branch; limits PENDING_DECISION | SHIFT_VIEW branch/global grant for admin list; OWN không tự mở branch list. Read-only consistent query, no business audit; row DTO versions for edits. Tests branch/OWN denial, filtering, inactive visibility policy |
| Roster GET `/roster?fromDate=&toDate=&departmentId=` | from<=to; department active/same branch; weekly default/start-day/range limit PENDING_DECISION; no client actor ID | SHIFT_VIEW branch/global for administrative roster; dedicated self query remains B1.1. Read-only tx; no CREATE/UPDATE audit. Tests weekly boundary, overnight spill, empty period, cross-branch staff/context, current session/revoke |
| Assign POST `/assignments` | {employeeId,templateId,date,departmentId,operationId}; active eligible employment and branch access, department/template same scoped branch; materialize concrete UTC window under approved time rule | SHIFT_MANAGE; generated WorkShift and Assignment identity+versions; global employee overlap across branches under SQL locks; unique pair conflict409; CREATE audit+affected-user intent. Tests duplicates, simultaneous competing shifts, overnight, source/membership revoke after wait |
| Edit assignment PUT `/assignments/{id}` | {employeeId,templateId,date,departmentId,reason,operationId}; before/after affected employee set; referenced started/historical work policy PENDING_DECISION | SHIFT_MANAGE resource->actual branch; If-Match assignment and explicit materialized WorkShift revision if edited (version aggregate contract PENDING_DECISION). Atomic source/assignment+audit+intents; tests reassignment, stale, global overlap, recipient old/new, UC139 race |
| Cancel future POST `/assignments/{id}/cancel` | {reason,operationId}; SQL server clock vs concrete ScheduledStartAt; preserve source/history; Workday/Interval references and began-shift policy PENDING_DECISION | SHIFT_MANAGE; If-Match assignment, transition to approved CANCELLED mapping, no deletion. Same User locks as UC139 Start, UPDATE audit+original employee intent; tests at/before/after start, concurrent Start/Cancel, stale, already-cancelled replay, fault rollback |

Common errors proposed:400 malformed/invalid input;401 invalid/revoked/expired session;
403 restricted/missing permission/foreign scope;404 nonexistent **within authorized scope**
(foreign resource403/not disclosed);409 duplicate/overlap/state/replay-payload conflict;
412 stale strong version;428 missing required If-Match;503 missing contract/dependency/store.
Weak/malformed If-Match400. Exact error code strings/response catalogue need C1-B review.
Every writer: one same-DbContext caller transaction, server timestamps, actual conditional
rowversion mutation, immutable before/after audit and durable intent/result replay. No success
before commit. No ad hoc notification bypass or hidden post-commit loss.

## 3. Concurrency, time and started-work decisions

Before writer activation, decide template edits: **future-only new materialization** or
propagate to eligible future WorkShifts. Do not rewrite historical snapshots. Propagation
requires complete affected users and rechecks of every global overlap; never overwrite a
started Workday/Interval dependency. Pending decision cannot be replaced by arbitrary denial
and then called full SRS acceptance. Same for reassignment/cancellation of referenced records.

Acquire actor+all affected employee User locks ascending; re-read account/employment/session,
permission paths, role/access/branch/department/template/assignment after waits. Coordinate A1
User/time lease when racing UC139; source/range/replay locks need a jointly agreed order.
If discovered user set changes, rollback/retry, not extend a fixed lease out of order.
No serialized stale middleware snapshot alone grants access. Serializable reads retain grant
paths to commit; revocation committed before lock acquisition must be reflected. Concurrent
revoker following same locks waits until operation finishes, rather than retroactively revoking
a committed operation. Every assignment writer must follow the approved SQL lock protocol.

ShiftAssignmentConflictReader is only a read:81 new SQL cases do not prove atomic check+insert
or universal overlap prevention. Unique(shift,user) is not a time exclusion index. Overlap
proof for future writer requires simultaneous distinct-shift requests for same employee,
cross-branch/overnight, edit vs assign/cancel and rollback, not merely a lock primitive test.

Concrete UTC periods use half-open overlap (adjacency allowed; one-tick overlap detected).
This existing helper behavior was tested, but wall-clock template interpretation, same-time
24h vs invalid, local date rollover, effective date and week boundary remain PENDING_DECISION.
No payroll or break deduction is invented; shift duration never replaces task effort.

## 4. Authorization and Department Head

Actual source seed and current disposable SQL catalog agree: DEPT_MANAGER has
SHIFT_MANAGE/BRANCH and SHIFT_VIEW/BRANCH. Supervisors/maintenance lead/OM have branch view
paths; staff/storekeeper/etc have own view paths as cataloged. These are grants, not automatic
identity privileges. No Administrator/Owner shortcut or new role grants.

Proposed management binding uses PermissionService for existing SHIFT_MANAGE on an adapter-
resolved branch; roster uses SHIFT_VIEW with branch context. Actor is SQL-current, account
ACTIVE/nondeleted/employment ACTIVE, unrestricted session. Referenced employee must separately
be active and eligible in the target branch/department; DH's actor role is not limited to one
department. Global ALL grant must not permit a branch-less or invalid employee/template pair.
OWN can access only authoritative self resources, not all employees in a selected department.
Existing WorkdayPermissionResourceResolver supports B1.1 own resource semantics only; do not
silently expand it. Exact admin resource binding/delegation and no regression requires C1-D.

## 5. C1-A compatibility decision — not just three ALTERs

Task80 original proposal: row_version3 columns +3 entity partial properties +new mapping
partial +one DbContext call. **Unconditional activation is NO_GO on mixed/legacy schemas**:
EF materialization will require absent columns, including A1/B1.1 ShiftAssignment reads.
No protected DB was inspected to assert deployment compatibility.

Two explicit review choices; neither applied:

1. Original global mapping: coordinated deployment only after every intended target is
   approved/migrated; verify old/new binaries, partial upgrade, marker and replay. A source
   model change alone is insufficient. Original5 C# targets retained in Task80 scope.
2. Recommended for mixed-schema review: keep global entities/DbContext unchanged; future
   DEV1 feature-specific schema gate validates3 SQL rowversions before UC138 requests, read
   versioned records via EF Database.SqlQuery<T>, write parameterized UPDATE ... WHERE
   row_version=@expected / OUTPUT row_version on same DbContext transaction. Legacy targets
   return503 for UC138 while existing A1/B1.1 continue using current model. Proposed new files
   `Services/WorkTime/Shifts/ShiftSchemaContract.cs`, `ShiftVersionedRecords.cs`,
   `ShiftVersionedStore.cs`; no implementation in Task81. This is explicit SQL rowversion
   concurrency, not an EF tracked-token mapping; require approval of the revised contract and
   affected-row checks for every writer. No implicit ModelCacheKey/conditional global mapping.

Required SQL proposal still exactly3 additive rowversion columns; no new RBAC/migration
framework. Guard approved fresh trial identity/marker/session approval, transaction/XACT_ABORT,
metadata type/nullable/partial-install checks, approved version marker and stable replay.
Test populated legacy business-row preservation. Rollback old app with added columns retained
is the non-destructive proposal; drop columns only on empty owned trial under explicit review.
No blind rollback dropping populated concurrency evidence. All of this remains unexecuted.

## 6. Five gates

| Gate/status | Owner; affected files/tables | Existing evidence / missing decision | Acceptance / rollback |
|---|---|---|---|
| **C1-A GO_FOR_REVIEW** | Shared DB/EF, DEV1 producer;3 shift tables; Task80 five model targets OR new feature-versioned store option above | SQL/EF absence proven, FKs/indexes inventoried. Choose legacy strategy and approve exact additive DDL/mapping scope, marker/replay/rollback | Fresh/populated/partial/replay/stale conflict, old/new binary compatibility. Retain data/columns on populated rollback; empty-owned drop only. GO_FOR_REVIEW is not permission to ALTER |
| **C1-B NO_GO** | DEV1 WorkTime DTO/controller/service/store, eventual Program DI separately | Seven operation contracts prepared; propagation, overnight/equal-time, version aggregate, cancel/started-record, error catalogue and replay identity PENDING_DECISION | Full authenticated HTTPS CRUD/roster/cancel, races/audit/fault rollback. Disable unpublished feature/rollback app retaining history; no claim of full UC from reads |
| **C1-C BLOCKED** | DEV2 publisher/outbox/replay/worker/inbox; DEV1 adapter; shared contract/DI | Confirmed draft-only checkpoint, no implementation. Need approved event/type/recipient/replay/retention/delivery failure contract and actual source | Durable readable inbox, retry/restart/dedup, replay/conflict, atomic rollback. Stop dispatcher/producer safely retaining intents; no deleting queues or fake delivery |
| **C1-D GO_FOR_REVIEW** | DEV1 PermissionService adapter/feature scope; organization owners | Existing source+SQL grants confirmed. Approve administrative resource binding, eligible employee/department and global/self behavior; no role-name shortcut | ALL/BRANCH/OWN, cross-user/branch, employment/restricted/revoke during wait, preserved B1.1/cleaning semantics. Revert feature adapter registration only via approved diff, no catalog rewrite |
| **C1-E NO_GO** | DEV1 verification + DEV2 integration/release | Current read/math/auth/foundation checks executable; writer/versioned migration/audit/outbox/inbox integration missing | Execute release matrix after A–D decisions, real disposable SQL/HTTPS failures and regressions. Release hold; rollback feature/app without deleting histories/intents; no protected DB action |

## 7. Executed verification and limits

All commands/counts/exit codes/logs are in TASK81_VALIDATION.json. New executions:
81 SQL overlap checks from reviewed Task80 runner;13 SQL/EF metadata/unique-index/User-lock
readiness checks;422 existing time-math checks;199 authorization regression(93+43+49+14).
Two SQL sessions blocked on retained User lock with DMV evidence and released on rollback.
This proves the existing primitive only, not the unimplemented UC138 assignment writer.
Unique-pair duplicate INSERT is real SQL rejected and fixture rolled back; no schema changed.

Build uses --artifacts-path under Task81; no old output intentionally overwritten. Tests use
isolated artifacts and new receipt paths. SQL target is exact approved disposable
HotelOps_Canonical_20261104_063549_81d4bc64_Fresh with A1 marker before fixture work;
all fixtures rollback. No HotelOpsDB/protected DB. New SQL metadata contains no credentials.
Full UC138 public HTTPS, writer overlap race/lost-update, audit+outbox rollback, migration
replay and inbox delivery **NOT_RUN** because no writer/schema approval/DEV2 infrastructure.
Framework tests with0 cases are NOT_AVAILABLE, not PASS. NU1900 feed availability warnings
are reported, not suppressed. Final build/diff/postflight outcome in validation receipt.

## 8. Decision / Task82

Target completion is DONE — READINESS ONLY subject to final preservation checks, not UC138
completion or permission to implement. No ledger/status/earned-point change in Task81.
Request Task82 only to resolve/approve C1-A legacy strategy and C1-B/C1-D pending business/
authorization decisions, then prepare exact scoped implementation after approval; coordinate
C1-C with DEV2. Do not start Task82 or publish a partial UC138 writer from this task.

See TASK81_DEV2_CONTRACT_REQUEST.md, TASK81_CHANGELOG.md and machine-readable validation
for file inventory, safety caveats, hashes and final disposition.
