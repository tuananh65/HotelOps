# Current source evidence — DEV1

Worktree `D:\SEP490\Projects\HotelOps`; branch `feature/BE1-database-integration`; HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Gói có suffix _01 vì thư mục tên gốc đã tồn tại và được bảo toàn.

Đã capture preflight **năm worktrees**, HEAD/branch/index/status từng nơi. Bản hash trước đọc: PREFLIGHT.json, 2548 files trong src/docs/scripts/database/AGENTS, loại generated bin/obj/artifacts/node_modules/packages và chỉ output _01 này. Hash source được kiểm ngay trước/sau trích; postflight tiếp tục so lại. Không build/test/SQL/HTTPS trong coordination task.

PREFLIGHT_SUPPLEMENT.json bổ sung 746 files bằng relative-path scan cho hai worktrees nằm dưới parent obj mà absolute glob ban đầu loại ra. Không lấy source excerpts/decisions từ hai worktrees đó trước supplement. Combined baseline **3294 files**, kiểm lại toàn bộ ở postflight; không tuyên bố coverage ban đầu đã đủ khi chưa supplement.

| Worktree | Branch / HEAD rút gọn | Preflight status |
|---|---|---|
| D:/SEP490/Projects/HotelOps | feature/BE1-database-integration / 4a18732 | 668 porcelain lines, local patches quan trọng |
| .../HotelOps/obj/task11-dev2-rehearsal | integration/task11-dev1-dev2-rehearsal / 3688ba9 | 103 lines, có staged changes |
| .../HotelOps/obj/task12-pr7-resolution | integration/task12-pr7-conflict-resolution / d6ef057 | clean |
| D:/SEP490/Projects/HotelOps-AI-Loop | ai/hotelops-loop-test / 8e0b673 | clean |
| D:/SEP490/Projects/HotelOps-Codex-B | ai/codex-b / 4a18732 | clean; không có untracked DEV1 runtime của worktree chính |

Exact full HEAD/index SHA/status cho từng nơi trong PREFLIGHT.json và HANDOFF_VALIDATION.json. Cùng HEAD giữa main worktree và Codex-B **không** có nghĩa cùng runtime bytes. Không dùng clean Codex-B Program thay current DEV1 Program.

## Program.cs — baseline duy nhất để DEV2 tạo C01 diff

| Identity | Giá trị |
|---|---|
| Absolute path | D:\SEP490\Projects\HotelOps\src\HotelOps.Api\Program.cs |
| SHA256 actual bytes | 586299FB650342F7389BAECD615DD29ACC94352857021FC4391554FF9372A505 |
| HEAD Git blob | a67c1ba5df48e4114ae40794ed1c78a672bc8f54 |
| Index Git blob | a67c1ba5df48e4114ae40794ed1c78a672bc8f54 |
| Working-tree raw-byte Git blob | ece1f08d2a55624e76594e06c3157a30a3d904dd |
| Working-tree filtered Git blob | b0dad3db4a086707a503a01564b0c975270d456c |
| Git state | unstaged modified; HEAD→Index empty |

Raw/filtered Git hashes khác nhau do Git text filters/line endings; SHA256 luôn là bytes thực. Working-tree object IDs chỉ được tính, **không git hash-object -w**, không tạo Git objects/commit.

Evidence: PROGRAM_IDENTITY.json; PROGRAM_HEAD_TO_INDEX.diff; PROGRAM_INDEX_TO_WORKTREE.diff; PROGRAM_HEAD_TO_WORKTREE.diff; `src__HotelOps.Api__Program.cs.lines.txt` trong evidence/. C01 phải base trên **current bytes**, không copy patch từ Program Task84/85 hoặc HEAD rồi mất local registrations.

| Program lines | Thành phần hiện có | HEAD/index/current |
|---|---|---|
| 14 | StartupSchemaValidator | Registered current; exact status/diff xem IDs |
| 18–20 | AddDbContext HotelOpsDbContext / UseSqlServer config reference | Existing shared registration; không export literal connection value |
| 24 | CleaningMonitorService | Current DI; service/controller source untracked |
| 25 | RoomTypeDirectoryService | Task84 current DI; source untracked |
| 26 | TurnoverHistoryService | Task85 current DI; source untracked |
| 27 | BranchAdministrationService | Task86 current DI; source untracked |
| 28–29 | IWorkTimeTransactionParticipant→SqlWorkTimeTransactionParticipant và concrete alias | Current; concrete resolves từ chính interface, không duplicate scoped instance |
| 30–31 | OwnWorkdaySnapshotReader, WorkdayService | Current DI; source untracked |
| 57, 69–70 | ISqlSessionService; cookie infrastructure; antiforgery | Current SQL-backed session/auth configuration |
| 71–79 | HttpContextAccessor, CurrentUserAccessor, SQL permission source/resolver, PermissionService, custom authorization | Delegating Workday resolver giữ Cleaning resolver cho resource khác |
| 85–94 | Build và StartupSchemaValidator trước listener | Không bypass schema gate |
| 98–110 | Development-only public OpenAPI; authentication/invalid-cookie/CSRF/restricted/authorization/session-touch/controllers | C01 không được đổi middleware order |
| Không có | AddInterceptors/Notification publisher/reservation/worker/commit guard registration | Không coi đã tích hợp |

Line numbers đối chiếu excerpt là nguồn cuối cùng nếu bảng mô tả gộp block; không sửa source để khớp tài liệu.

## Source identities và local source quan trọng

`evidence/CURRENT_SOURCE_IDS.json` chứa 317 current C# paths, SHA256, raw Git blob, HEAD/index blob (null nếu untracked), git status và lineCount. Mỗi excerpt có original path, SHA256 và source line numbers. HEAD-only checkout không chứa toàn bộ DEV1 runtime đã nghiệm thu cục bộ.

| Nhóm local uncommitted | Current files / đóng góp được xác minh |
|---|---|
| Task82 | CleaningMonitorService/controller/DTO timeline pagination, vẫn PARTIAL vì Cancel thiếu |
| Task84 | RoomTypeDirectoryService/Schema + RoomTypeDirectoryController; Program registration hiện tại |
| Task85-B | TurnoverHistoryService/Boundary/Files/Schema + TurnoverHistoryController; Program registration hiện tại |
| Task86 | BranchAdministrationService/Schema/DependencyReader + controller; Program một DI addition; SQL forward chỉ proposal/trial evidence |
| A1/B1.1 | HotelOpsDbContext.WorkTime.cs, Workday/WorkInterval/TimeCorrectionRequest, WorkTime services/DTO/controller, resolver và DI đều có local source |
| P0/Fallback | SqlSessionPolicy.cs và Authorization registration có local patches, WeatherForecast sample bị xóa trong working tree |

Generated/shared HotelOpsDbContext.cs cũng có preexisting local patch; không scaffold hoặc ghi đè. Từng worktree khác có source baseline riêng, không trộn snippets từ worktree khác thành current runtime.

## Historical writers: immutable S, không runtime

S=`cf8fb020362f8f64192e7f2f14a10547cebe5b91`.

| S path | Git blob | Current checkout |
|---|---|---|
| Services/Housekeeping/HousekeepingAssignmentsService.cs | 1b9f0d49b1a8d4a7435db421733c6472e6864b11 | Missing |
| Controllers/HousekeepingWorkflowController.cs | f2b771332a2cdb8942c809bfcd339c6d68df6550 | Missing |
| Services/Housekeeping/InspectionExecutionService.cs | 87e1a7d6bbc5496d850253d7a84208de64326ac6 | Missing |
| Services/HotelStructure/RoomTypeBatchService.cs | 8aba8703a8e26808af57208cdd7439ceb43c099e | Missing |
| scripts/database/release/housekeeping/01_Housekeeping.sql | 622b64f51b2767575575b56a4e9530225cd2534b | Missing |

Paths Services/Controllers có prefix src/HotelOps.Api/. Exact numbered historical snippets và metadata trong HISTORICAL__*.lines.txt / HISTORICAL_SOURCE_IDS.json. Chưa restore, chưa bật producer. Class trong assignment file là partial `HotelOps.Api.Services.HotelStructure.RoomTypeCreationService`, không phải class mang tên filename.

## Task79 receipts, chỉ chứng minh lịch sử

| Exact path dưới scripts/verification/Task79/ | PASS | FAIL / NOT_RUN | Giới hạn |
|---|---:|---|---|
| auth-expiry-after.local.json | 43 live HTTPS/SQL checks | success=true; after receipt không có failed case | Production Program thật; SQL datetime2 và cookie/session windows |
| boundaries-final.local.json | 53 deterministic assertions | Final receipt đạt; stale-assembly probe lỗi trước đó được report giữ | 20 timezone model cases; không chạy OS UTC thật |
| user-sql-regression.local.json | 26 SQL participant checks | success=true | Credential participant, không chứng nhận UC004 HTTP endpoint |
| TASK79_VALIDATION.json | 199 isolated security checks; build PASS | Framework 0; UTC OS NOT_RUN; Notification/PauseAll NOT_RUN | 93/43/14/49 core/doubles; 2 NU1900 |
| TASK79_CLOSURE_VALIDATION.json | Closure preservation | Không chạy lại build/SQL | B12/source/approval provenance |

Catalog ghi trong receipt: `HotelOps_Canonical_20261104_063549_81d4bc64_Fresh`. Receipt không chứng minh instance; không suy instance từ user secrets hoặc source harness. Coordination task không kết nối lại SQL để lấp khoảng trống này. Hash/selected fields của các receipt nằm trong RECEIPT_SUMMARIES.json.

B12 exact path `scripts/verification/Task79/B12_APPROVAL_SCOPE.md`; current SHA256 đã kiểm trực tiếp và khớp closure hash **B912CFCE7DCCF5B5AF1596958DEE2B6CA22204B61DB93F9A6B4F9E40965CA8E0**. Status B1.2 NOT APPROVED/NOT APPLIED; A1/B1.1 đã duyệt không bị mở lại.

Ba receipt cuối Task79 được copy byte-identical sau kiểm schema keys/nội dung an toàn vào evidence/RECEIPT__Task79__*.json; RECEIPT_SUMMARIES.json giữ path/hash nguồn. Case labels không phải credential/session values. Receipt Task86 cũng giữ riêng từng lượt lỗi/thành công, không gộp số checks thành unique acceptance total.

## Các pathway đã tìm trực tiếp

CURRENT_COMMIT_PATHWAYS.txt liệt kê line hits toàn current src cho EF SaveChanges, ExecuteUpdate/ExecuteSql (không đi qua SaveChanges), Begin/Commit/Rollback, raw ADO trên GetDbConnection/GetDbTransaction, ambient checks và savepoint/transaction identifiers.

Source hiện có nhiều caller-owned EF transaction và raw ADO command cùng transaction. Không có interceptor registered. Không tìm thấy `new TransactionScope`, explicit savepoint calls hoặc independent `new SqlConnection` trong current production source; đây là kết quả search bounded, không bảo đảm tương lai/external code không tạo pathway đó. EF implicit savepoint không bị loại trừ chỉ vì grep không thấy. C01 phải có bypass tests, không lấy search này làm chứng nhận universal guard.

Local refs DEV2 deactivation/maintenance-returnwork có evidence User responsibility coordination; không phải proof Branch Active protocol hoặc PM Pause All đã được tích hợp. Ref identity/search scope trong REF_EVIDENCE.json; không fetch/checkout/merge.
