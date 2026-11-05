# Task71 — Accelerated Batch 2

31/10/2026, Asia/Saigon. DEV1; branch `feature/BE1-database-integration`.

## A. Batch và requirement review

Đối chiếu AGENTS, SRS/Permission Matrix hiện lưu, Change Notes, delivery plan,
Task69/70, DbContext/entity/FK/index metadata và harness. Không tìm thấy bản matrix
final mới; không suy diễn quyền Reception/DH. Quyền quản trị master data tiếp tục
là SQL-backed `MASTER_DATA_MANAGE`, không role-name bypass hoặc grant mới.

| UC — tên đúng SRS | Lý do chọn / điều kiện |
|---|---|
| UC019 Deactivate Room Type | Nối UC017/018/020, active-room guard, reason/confirmation/ETag, soft deactivate/audit; catalog/branch locking có sẵn |
| UC021 Update Room Information | Nối UC020; floor/area/type/number/reason, giữ Room identity/status/open work/history, version/audit |
| UC024 Create Department | Schema có sẵn, branch code uniqueness, optional DH; chỉ triển khai sau quyết định V1 của developer |
| UC025 Update Department | Dùng chung manager validation/lock và DTO; active department/version/audit, không sửa ownership/code |

Không chọn UC006. UC016 vẫn PARTIAL: chưa có evidence mới giải quyết DEV2 writer
blockers Task66. UC012 không được coi là một PUT đơn giản: SRS còn Create Branch và
Deactivate Branch/GBR033, ảnh hưởng dependency writers; không cắt flow để cộng điểm.
UC022 cần room-status/DEV2 orchestration. UC039/040 còn chênh lệch applicable-room-types
plural với một `RoomTypeId` trong schema, không thêm mapping/schema ngầm. Không cộng UC
đã hoàn tất ở Task70. Bốn UC trên có acceptance độc lập.

## B. Quyết định ASSUMED_V1 được developer duyệt

Trong Task71 developer đã duyệt rõ:

- `departments.manager_user_id` là nhân viên active có role SQL `DEPT_MANAGER` còn
  hiệu lực và thuộc cùng branch. V1 kiểm tra account ACTIVE, employment ACTIVE,
  nondeleted; active role/assignment tại branch, AssignedAt không ở tương lai,
  ExpiresAt chưa hết hạn; membership `user_branch_access` đã granted/chưa revoked.
- Một DH có thể quản lý nhiều department trong cùng branch; không tạo unique
  constraint trên manager_user_id. Không xem DH là trưởng riêng một department.
- Designation không tạo/mở rộng RBAC; người thao tác vẫn cần permission/scope SQL.
- Kiểm tra manager lúc bắt đầu và ngay trước commit; chống race disable/role/branch
  membership changes. Không đổi legacy hàng loạt, không tự gán nhân sự theo ID.
- Ưu tiên explicit SRS khi có mâu thuẫn. GBR041 cho phép một manager/department,
  không cấm cùng manager ở nhiều department; phù hợp V1 đã duyệt. Toàn bộ matrix
  và pending Reception policies không được coi là đã phê duyệt bởi quyết định này.

Các default V1 khác: room number alphabet/normalization/notes như Task70; room edit
reason10–1000 ký tự không control characters, cùng pattern structure update. Department
code1–30 ASCII letters/digits/_/-, bắt đầu letter/digit; name1–150; description≤4000.
PUT department là full replacement name/description/optional manager; null manager là
yêu cầu xóa designation, không sửa role/membership. Code/branch/status không nhận từ PUT.

## C. Implementation và production API

| Endpoint | UC / behavior |
|---|---|
| POST `/api/room-types/{id}/deactivate?branchId=` | UC019: If-Match, confirmation/reason;200 DTO,409 inactive/active-room dependency DTO,412 stale,428 missing If-Match |
| GET `/api/rooms/{id}/edit?branchId=` | UC021: scoped editable DTO + strong ETag |
| PUT `/api/rooms/{id}?branchId=` | UC021: If-Match, fields/reason;200 DTO/ETag; status/history không thay đổi |
| POST `/api/branches/{branchId}/departments` | UC024:201 DTO/ETag; unique code; optional eligible manager |
| GET `/api/branches/{branchId}/departments/{id}` | UC025: details + strong ETag |
| PUT `/api/branches/{branchId}/departments/{id}` | UC025: If-Match, name/description/manager;200 DTO/ETag |

Tất cả giữ HTTPS, cookie auth, CSRF và no-store. Invalid fields/reference400,
anonymous401 (với valid CSRF cho mutation), permission/cross-branch403, missing scoped
record404, conflict409, stale412, missing precondition428, contention/provider503.
Branch không tồn tại trả403 từ permission gate trước query, không đổi thành probe404.
Missing CSRF400; unknown JSON/status/ownership fields bị từ chối. Unexpected injected
audit SQL failure trả generic500 và rollback; không lộ provider/credential data.

Tái sử dụng service registration/transaction helper hiện có, không sửa DI/shared auth:

- Actor và manager Users được khóa theo ID tăng dần **trước** branch/catalog/entity
  locks. Manager role/membership đọc dưới SERIALIZABLE; các eligibility lock được giữ
  tới commit. Commit-time clock/session/grant/manager validation ngăn expiry race.
- UC019 dùng cùng catalog/branch boundary UC020/021; không commit deactivation khi
  active Room còn dùng type. Dependency DTO cũng qua final session/grant gate.
- UC021 giữ RoomId/BranchId/OperationalStatus/IsActive và mọi FK từ work/history.
  Floor/area/type phải active/scoped; chỉ cho giữ nguyên legacy global type đã gắn
  với room, không cho chọn một global type mới hay reparent branch. Inconsistent
  legacy parent/type relationships fail closed thay vì disclose foreign metadata.
- ETag hash full writable state + audit revision; room tag còn gồm persisted updated_at
  được reread sau UPDATE để không sai vì SQL datetime rounding. Không thêm rowversion
  vào generated entity. Native external writers vẫn phải tuân thủ eligibility protocol.
- Department operations chạy được trước UC017 migration; không ép schema RoomType
  V1 cho workflow không liên quan. Không migration/schema/generated-file change.
- Business row + reason/before/after audit cùng transaction, tracker sạch trước final
  session gate, cancellation/provider failure giải phóng transaction.

Tệp tạo: `DTOs/HotelStructure/Batch2Dtos.cs`,
`Services/HotelStructure/RoomBatch2Service.cs`, `DepartmentBatch2Service.cs`,
`scripts/verification/HotelStructure/Task71BatchCases.cs`, `Task71SqlCases.cs`, báo cáo này.
Tệp sửa: `Controllers/RoomTypeBatchController.cs`,
`Services/HotelStructure/RoomTypeBatchService.cs`, verification `Program.cs`, delivery plan.
Các path source tương đối với `src/HotelOps.Api`. Không thay unrelated module/DEV2.

## D–F. Acceptance, SQL/HTTPS evidence và regression

[Receipt cuối](task71-complete.local.json): **404 PASS** trên
`HotelOps_Canonical_20261031_064226_579d7686_Fresh`:2 startup +5 legacy compatibility
+181 Task70 regression/setup +216 Task71 batch. Không suy test PASS từ build/test exit0.

| UC | Final independent acceptance |
|---|---|
| UC019 | **50 PASS**:41 HTTPS/fixture/persistence +9 post-write SQL assertions; active-room blocker/list, reason/confirmation/If-Match, soft deactivation/atomic audit/rollback, concurrent room-create/deactivation consistency |
| UC021 | **50 PASS**:41 HTTPS/persistence +9 post-write SQL assertions; typed editable GET/PUT, scoped/missing/inactive reference rules, retained room identity/status/history/open cleaning FK, foreign-parent legacy404, stale/concurrent writes and audit rollback |
| UC024 | **53 PASS**:32 HTTPS/persistence +18 SQL final-boundary +2 DMV/competing-manager assertions +1 legacy-schema creation assertion; role/membership/account eligible manager, optional manager, same DH across departments, duplicate create race, atomic audit/rollback |
| UC025 | **51 PASS**:31 HTTPS/persistence +18 SQL final-boundary +1 legacy-schema update +1 no-RBAC-change snapshot; versioned metadata/manager update, inactive/foreign/missing/invalid/stale, concurrent PUT and audit rollback |

**204 independent checks**; common login/fixture/schema checks are separate, not duplicate UC
completion. Grouping uses literal receipt labels, excluding shared `UC024/025 compatibility`
assertion from the UC024 subtotal. Header `batch71Cases` records only each HTTP/persistence phase;
SQL and legacy/RBAC checks are in the full `cases` array and counted above.

| Shared regression / validation | Actual result |
|---|---|
| UC017 | **163 PASS**,10 DMV waits,7 post-INSERT schedules,12 actual deployment entry calls in [full560 receipt](task71-accepted.local.json); no double count of repeated Task70/71 sections |
| UC018/142/020 | **180 PASS +1 explicit deployment fixture check** in final404; real HTTP/SQL scopes, defaults, count/page, version/audit/fault/cancel/expiry and create uniqueness preserved |
| UC013/014/015 | [590 PASS](task71-structure-regression.local.json) =216+159+215,14 UC015 post-UPDATE fault schedules |
| UC023 | [229 PASS](task71-rooms-regression.local.json) =227+2 startup; real HTTPS directory/detail/reasons/history,4 DMV waits, cancel/timeout/final-expiry behavior |
| Auth foundation | **174 PASS** =43 PermissionService +61 Authorization +49 ResourceAuthorization +21 Hardening; existing doubles/loopback limits retained, live SQL evidenced by actual API acceptance |
| Solution build | Exact `dotnet build HotelOps.slnx` **PASS**,0 errors,2 NU1900; `obj/task71-build-last.log` |
| Harness build | **PASS**,0 errors,2 NU1900; `obj/task71-harness-last.log` |
| dotnet test | `dotnet test HotelOps.slnx --no-restore` exit0/no test project discovered: **NOT RUN** for test-framework suite; console suites actually run separately |
| Diff/preservation | **PASS**: diff exit0,562 original files/562 backups checked,0 unexpected changes/hash failures; HEAD/index/four worktrees unchanged; new source/report whitespace clean. Evidence: `obj/task71-validation.local.json` |

Represented nonduplicated suite slices: **1560 PASS =404+163+590+229+174**. Do not sum
every intermediate receipt: initial missing-branch expectation failure, subsequent386,
full560 and intermediate399 checks remain in separate logs/trials/receipts. UC017 and
consumer regression ran before the last UC021-only defensive filter/harness delta;
the new404 receipt validates that delta without rerunning unrelated full suites.

SQL direct-service schedules dùng production PermissionService/EfPermissionDataSource/
SqlSessionService với persisted sessions và thật SQL; interceptor chỉ inject lỗi sau
khi query trong transaction chứng minh business write đã xảy ra. Không fake quyền.
Mỗi UC kiểm tra late session expiry, actor grant expiry, cancellation và snapshot
rollback/context cleanup. Department thêm manager role expiry, membership revocation,
account disable ngay sau business write. Một competing manager writer thật được DMV
quan sát chặn operation; sau commit revocation, operation reread và từ chối designation.

HTTP acceptance dùng root production Program/Kestrel, pinned trial TLS, real credential
login/cookie/CSRF/SQL. Kiểm tra success/401/403/cross-branch/invalid/missing/business
violations/persistence/audit; real audit-trigger faults; stale/replayed If-Match;
concurrent PUT200/412; duplicate department POST201/409; deactivation versus room
creation race không để lại active room trên inactive type. Không coi SQL interceptor
cancellation là HTTP disconnect. Những transport/provider faults có regression riêng.

## G. Blockers, warnings và giới hạn

- UC016/006 không đổi; UC012/022/039/040 không được tính vào batch.
- Build đầu có sai navigation `User.UserRoles`; sửa truy vấn `db.UserRoles` theo mapping.
  Một rebuild harness khi DLL đang được chạy gặp MSB3026/3027/3021; không dừng/ghi đè
  process; rebuild sau khi process hoàn tất. Các log lỗi được giữ, không count PASS.
- NU1900 do không tải được NuGet vulnerability feed; không khẳng định đã kiểm tra
  vulnerability thành công. Không có test-framework project trong solution.
- Harness đầu có EF1003 tại deliberate SQL fault fixture; thêm closed entity allowlist
  và validated construction, final build chỉ còn NU1900. Final canonical bị chờ
  RESOURCE_SEMAPHORE tại DBCC trên SQL Express; không đổi server settings/timeout,
  không kill session. Sau đó cả Fresh/Upgrade/AuthUpgrade CHECKDB/schema comparison PASS.
- Chỉ disposable SQL; không HotelOpsDB/shared/production deployment. Không kiểm chứng UI,
  future task/inspection writers hoặc mọi external SQL writer không tuân thủ protocol.
- Không triển khai Department Head permission mới; designation chỉ là dữ liệu được
  developer duyệt, không thay thế SQL-backed authorization của các consumer.

## H–I. Classification và tiến độ

**Cả UC019, UC021, UC024, UC025: COMPLETE_VERIFIED_V1.** Build và console acceptance
đã PASS; không biến no-tests exit0 thành testing evidence. Task70 baseline14/75,
32/256=12.50% → **18/75 (24%),41/256=16.02%**. Trọng số3+2+2+2=9; delta **+4UC/+9
points/+3.52 percentage points weighted**, remaining57UC/215points. Taxonomy18 COMPLETE_VERIFIED
(including V1),5 PARTIAL,33 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75. UC016 vẫn PARTIAL.
Không cộng lại UC017/018/142/020/023 hoặc giảm mẫu số75/256.

## J. Batch 3 đề xuất

Ưu tiên chốt đầy đủ UC012 (branch create/update/deactivate dependency protocol), rồi
UC048 View Room Inspection History và reviewed UC039/040 checklist ownership/version
contract. UC022 chỉ chọn khi room-state/DEV2 guards đã thống nhất. Không mở UC006 hoặc
đánh dấu UC016 complete từ các guard của room/type trong batch này.
