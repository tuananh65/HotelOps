# Task70 — Hotel Structure Batch 1

Ngày 31/10/2026, Asia/Saigon. DEV1, `feature/BE1-database-integration`.

## A. Chọn batch và preflight

Đã đọc AGENTS, delivery plan, SRS Reading Guide/Change Notes, UC specifications,
functional screens, GBR, Permission Matrix, database rules/schema/mappings và
Task68/69 cùng harness. SRS/Permission Matrix vẫn là draft; không bổ sung grant,
role, Reception transition hoặc chính sách Department Head. Tái sử dụng quyền
SQL `MASTER_DATA_MANAGE` đã dùng cho Hotel Structure/UC017. Không tìm thấy tệp ERD
riêng trong repository; đối chiếu quan hệ từ schema SQL và DbContext/FK metadata.

| UC và tên đúng theo SRS | Yêu cầu/quyền/phạm vi | Hiện trạng, phụ thuộc, độ khó và quyết định |
|---|---|---|
| UC018 Update Room Type | Active type, version, defaults/code validation, atomic audit; MASTER_DATA_MANAGE tại branch | Chọn ưu tiên 1; tái sử dụng UC017 BranchV1, checklist và catalog lock; thấp–vừa; acceptance độc lập |
| UC142 View Room Types List | Code/name/status/branch, active-room count, sort/page, scoped detail; cùng quyền quản trị | Chọn; blocker branch ownership cũ được Task69 giải quyết cho V1; thấp; acceptance độc lập |
| UC020 Create Room | Active branch/floor/type, optional area, unique room number, DIRTY + initial history + audit; cùng quyền quản trị | Chọn; Room/history schema có sẵn, không cần DEV2 writer; vừa; acceptance độc lập |
| UC012 Update Branch Information | Branch validation/uniqueness/version/audit; management scope | Chưa có writer; có thể tái sử dụng pattern sau batch này; thấp–vừa |
| UC016 Deactivate Floor/Area | Dependency guards, reason, concurrency/audit | Loại: PARTIAL, DEV2 writer defects Task66 chưa được giải quyết |
| UC019 Deactivate Room Type | Active-room guard, confirmation/reason/version, soft deactivate/audit | Chưa có writer; hợp batch sau với UC021 và UC020 lock protocol; vừa |
| UC021 Update Room Information | Location/type validation, version/reference preservation/audit | Chưa có writer; cần khóa/ownership allowlist; vừa |
| UC022 Deactivate Room | Open cleaning/inspection/incident/block guards, reason/history/audit | Chưa có writer; phụ thuộc orchestration/DEV2; cao, không chọn |
| UC024 Create Department | Branch-scoped department validation/uniqueness/audit | Chưa có writer; không suy diễn quyền DH; vừa |
| UC025 Update Department | Validation/version/manager references/audit | Chưa có writer; cần đối chiếu assignment/manager rules; vừa |
| UC026 Deactivate Department | Account/work/asset/reference guards, reason/audit | Chưa có writer; DEV2 dependencies, cao |

UC013/014/015/017/023 đã hoàn tất, chỉ regression; không cộng lại điểm. Không chọn
UC006. Không sửa shared mapping, DI, schema, generated entities, catalog hoặc DEV2.

Preflight sao lưu/hash **517** tệp dirty/untracked tại `obj/task70-preflight/` và
`obj/task70-preflight-manifest.json`, ghi index/worktrees/HEAD trước khi sửa. HEAD
ban đầu `4a187320643d0c82f232c6b9dd5a667b8401c280`; không có Git mutation.

## B. Quy tắc và phạm vi V1

- UC018: DTO riêng, SQL session/grant kiểm tra đầu/cuối, branch ownership và capacity
  bất biến, legacy global không bị chuyển đổi. Name/description/positive cleaning
  minutes và generic active checklist có effective PUBLISHED version/active criterion.
  Mã tùy chọn được chuẩn hóa như UC017; unique trong branch và không chiếm mã legacy.
  UC overview có nhắc sửa code, detailed flow tập trung defaults: hỗ trợ tùy chọn code
  để đáp ứng cả hai, bỏ code thì giữ nguyên. Room ID/FK, inspection version/result giữ nguyên.
- UC142: không ghi business/audit; SQL scope filtering trước count/page; branch filter
  hoặc tự lấy các branch được cấp quyền. Global ALL mới thấy legacy ownership NULL.
  Active-room count, search literal code/name, status, ổn định theo code/branch/id;
  sort code/name/status, descending tùy chọn; default **20**, max100. Detail trả
  DTO/action `canUpdate`, không quảng cáo endpoint UC019 chưa được triển khai.
- UC020: branch/floor/type phải active và cùng branch; optional area active, cùng
  branch và floor nếu area có floor. Chỉ scoped V1 type cho phòng mới; legacy giữ
  nguyên đối với phòng hiện hữu. Room number unique trong branch kể cả inactive.
  Khởi tạo `DIRTY` (SRS Pending Cleaning), history `from=NULL/to=DIRTY/trigger=INIT`,
  actor từ session, UTC, audit và Room cùng một transaction. Không nhận status từ caller.
- ASSUMED_V1: room number dùng cùng alphabet ASCII letters/digits/underscore/hyphen
  của code, trim/uppercase, tối đa30; notes tối đa4000. Không thêm lifecycle transition.
  Giữ generic-checklist/legacy policy Task69; không backfill, copy hay reparent type.
- Serializable transaction; actor User lock → branch lock → UC017 catalog application
  lock → references/business rows. Shared SQL context luôn sạch trước session gate.
  Strong ETag hash full type state + audit revision + active-room count, không thêm
  rowversion giả vào entity. Retained reads và audit revision bảo vệ If-Match/ABA
  cho writer tuân thủ protocol. External writers phải duy trì cùng eligibility rules.

## C. Production endpoints

| Method/route | Contract và status chính |
|---|---|
| GET `/api/room-types?branchId=&isActive=&search=&page=&pageSize=&sort=&descending=` | UC142; branch optional;200 page/filter DTO;400 invalid;401/403;503 schema/provider unavailable |
| GET `/api/room-types/{id}?branchId=` | UC142/018 detail + strong ETag; scoped type cần branch; global legacy detail bỏ branch;200/401/403/404/409/503 |
| PUT `/api/room-types/{id}?branchId=` | UC018; name/minutes/checklist/description/optional code; strong If-Match;200/400/401/403/404/409 duplicate/inactive/412 stale/428 missing precondition/503 |
| POST `/api/rooms` | UC020; branch/floor/type/number/optional area/notes;201 DTO;400/401/403/404/409/503 |

HTTPS, existing cookie authentication và CSRF middleware; missing CSRF400. Unknown
JSON fields bị từ chối. Responses no-store, không expose EF entities hoặc provider
exception. Deliberate unexpected audit SQL fault trả generic500 và rollback; không
đổi lỗi lập trình/schema thành thông báo thành công hoặc retry tự động.

## D. Tệp nguồn

Tạo:

- `src/HotelOps.Api/Controllers/RoomTypeBatchController.cs`
- `src/HotelOps.Api/DTOs/HotelStructure/RoomTypeBatchDtos.cs`
- `src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs`
- `src/HotelOps.Api/Services/HotelStructure/RoomCreationService.cs`
- `scripts/verification/HotelStructure/Task70BatchCases.cs`
- `scripts/verification/HotelStructure/Task70SqlCases.cs`
- Báo cáo này.

Sửa: `RoomTypeCreationService.cs` chỉ thêm `partial`, giữ nguyên UC017;
verification `Program.cs` thêm `--task70`/`--task70-only` và receipt fields;
`scripts/verification/DEV1_21_DAY_DELIVERY_PLAN.md` cập nhật bằng chứng/tiến độ.
Không tạo project kiến trúc mới. Các partial dùng lại DI hiện hữu.

Receipt mới ở HotelStructure/CanonicalDatabase evidence; log/build/preflight và
diagnostic tạm ở ignored `obj/task70-*`. Không ghi credential/token/password vào receipt.

## E–G. Acceptance SQL/HTTPS và concurrency/rollback

[Receipt cuối](task70-accepted.local.json): **345 PASS**, database
`HotelOps_Canonical_20261031_061446_c9b9a07c_Fresh`.

| Phạm vi | Kết quả thực tế |
|---|---|
| UC018 | **64 PASS =55 HTTPS/persistence +9 SQL final-boundary**; edit/tag, scoped success,401/403/404, legacy isolation, missing/weak/stale If-Match, invalid fields/checklist/code, duplicate legacy code, inactive type, unknown ownership field, CSRF, optional code normalization, historical Room/inspection version preservation, atomic audit, two concurrent PUT200/412, real audit-trigger failure/rollback |
| UC142 | **49 PASS =40 HTTPS/persistence +9 SQL final-boundary**; explicit/automatic branch scope, global pagination/count, sorting, status, empty filters, invalid pagination/sort/status,401/403, legacy NULL details/global-only access, all reads preserve business/audit |
| UC020 | **55 PASS =46 HTTPS/persistence +9 SQL final-boundary**; valid scoped creation,401/403, cross-branch/missing/inactive type/floor/area, legacy new-assignment rejection, invalid number/status injection, CSRF, normalized SQL Room, one initial history/audit, branch uniqueness/replay, audit-trigger rollback of all three records, explicit successful retry, same-number race201/409 |
| Batch setup | **12 PASS**: actual credential login/CSRF và tạo hai type qua production UC017 |
| UC017/startup regression | **165 PASS =163 UC017 +2 startup**,10 DMV waits,7 real post-INSERT schedules;12 deployment entry-point calls, legacy/fresh/replay/recovery, metadata/Room guards, session/grant/cancel/audit/1222/-2/1205 rollback |

SQL final-boundary tests dùng persisted session, real PermissionService/EfPermissionDataSource/
SqlSessionService và production services; interceptor **không** thay quyền/session provider.
Mỗi UC có3 lịch: session expiry, grant expiry, cancellation sau UPDATE/INSERT hoặc
list page. Query trong transaction chứng minh business row đã được ghi trước probe.
Mỗi lịch kiểm tra rejection, toàn bộ type/room/history/audit snapshot không đổi và
transaction/tracker sạch. **27 assertions** này không được gọi là HTTP transport faults.
Các assertion HTTPS chạy actual root Program/Kestrel, pinned self-signed TLS, login,
cookie, CSRF và SQL trial. Không suy acceptance từ `dotnet test` hoặc build.

## H. Shared regression/build

- `dotnet build HotelOps.slnx`: **PASS**,0 errors,2 NU1900 warnings (NuGet vulnerability
  feed không truy cập được). Harness build PASS,0 errors,2 NU1900.
- `dotnet test HotelOps.slnx --no-restore`: exit0 nhưng solution chỉ có API, không có
  test project/discovered tests: **NOT RUN** cho test-framework suite. Console suites
  dưới đây được chạy riêng, không đổi exit0 thành test evidence.
- Auth foundation **174 PASS** =43 PermissionService +61 Authorization +49
  ResourceAuthorization +21 Hardening. Giữ giới hạn test doubles/loopback đã công bố;
  live SQL authorization evidence nằm trong HTTPS/SQL acceptance, không suy từ doubles.
- [UC013/014/015 regression](task70-structure-regression.local.json): **590 PASS**
  =216 UC014 +159 UC013 +215 UC015; migrated trial,14 post-UPDATE fault schedules.
- [UC023 regression](task70-rooms-regression.local.json): **229 PASS** =227 UC023
  +2 startup; migrated trial,4 DMV waits, actual production HTTPS list/detail/reasons/
  source/history/branch/expiry/cancel/timeout behavior preserved.
- Tổng các lượt cuối: **1338 PASS =345+590+229+174**. Không cộng lượt thử trước hay
  SQL catalog assertions trong canonical receipts vào con số này.
- `git diff --check`: PASS, exit0; các LF/CRLF notices tồn tại trong dirty baseline.
- Preservation evidence: `obj/task70-validation.local.json` kiểm tra hash517 tệp gốc
  và517 bản sao, chỉ cho phép3 tệp có sẵn trong scope thay đổi; kiểm tra HEAD/index,
  cả4 worktrees và whitespace của source/report mới. Không commit/push hay xóa artifact.

## I. Lỗi đã gặp và giới hạn

Giữ toàn bộ log/trial cũ, không relabel lỗi thành PASS:

1. Sandbox Windows token gây SSPI error trước SQL; retry ngoài sandbox thành công.
2. Trial không có marker bị rowversion guard từ chối; bổ sung marker chỉ cho đúng
   database disposable rỗng theo harness convention, rồi chạy prerequisite có sẵn.
3. Harness thiếu namespace ASP.NET gây compile error; đã sửa, final build PASS.
4. Fixture CSRF kỳ vọng403 thay vì400 và anonymous POST thiếu CSRF bootstrap; đã sửa.
5. UC018 final session gate phát hiện context còn tracked audit sau SaveChanges;
   sửa detach/clear trước gate; final HTTP/SQL success và expiry/cancel rollback PASS.

UC016 còn PARTIAL; UC006 gate không mở. Không truy cập HotelOpsDB, không migration
production/shared/startup, không schema/generated-file/DEV2/catalog changes. Chưa
triển khai production, chưa kiểm chứng UI hoặc UC028/inspection writer tương lai
tiêu thụ updated defaults; chỉ xác minh persisted contract và lịch sử hiện hữu.
ETag không thay thế protocol của external writers hoặc một native rowversion schema.

## J–K. Phân loại và tiến độ

**UC018, UC142, UC020 đều COMPLETE_VERIFIED_V1**, acceptance độc lập và shared
regression đều PASS. Baseline11/75,27/256 → **14/75 UC (18.67%),32/256=12.50%**;
gain **+3UC/+5points**, remaining224 points. Không cộng lại UC017/023. Taxonomy:
14 COMPLETE_VERIFIED (including V1),5 PARTIAL,37 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75.
Implementation completed, build passed và actual console acceptance passed là ba
kết quả riêng; không gọi `dotnet test` exit0/no-tests là testing passed.

## L. Batch 2 đề xuất

UC019 Deactivate Room Type +UC021 Update Room Information +UC012 Update Branch
Information: tái sử dụng session/branch/catalog lock, ETag và audit. Đối chiếu riêng
requirements/ownership/reference rules trước coding; không mặc định có quyền đổi
branch hoặc mutate legacy ownership. UC016/022/026 tiếp tục chờ dependency review.
