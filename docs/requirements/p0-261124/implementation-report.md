# P0 Project 2 — báo cáo triển khai và kiểm chứng

> Đính chính phạm vi nghiệm thu, 24/11/2026 15:49: **PARTIAL đối với yêu cầu hoàn tất toàn bộ cả A và B**. Các kết quả test bên dưới vẫn hợp lệ trong phạm vi đã chạy; chúng chưa chứng minh từng tiêu chí của cả hai tài liệu. Đối chiếu lại source cho thấy popup Tạo yêu cầu chỉ hỗ trợ refill/replenishment; yêu cầu thông thường và sự cố phòng còn mở bằng các luồng/tab riêng, chưa đầy đủ thiết kế unified dynamic form ở B §17. Bộ A có 15 nhóm test riêng, chưa có ma trận bằng chứng hoàn chỉnh từng tiêu chí (đặc biệt giao việc cho cấp dưới, hồi quy tất cả role và các ngoại lệ transfer). Cần đóng các mục này trước khi xác nhận hoàn tất 100%; không dùng 28 nhóm B để thay toàn bộ nghiệm thu A.

Cập nhật 24/11/2026 (Asia/Saigon). Source và migration đã triển khai trên TEST. Bằng chứng cộng dồn: 28/28 nhóm E2E, 1.349 assertions; 5 nhóm bổ sung; 8 nhóm điều chuyển kho; 100 lượt browser desktop/mobile và 6 kiểm tra Action Center/báo cáo. Xem [bằng chứng chi tiết](validation-evidence.md).

Đây không phải xác nhận deploy Production, cũng không phải tuyên bố mọi tổ hợp nghiệp vụ đã được kiểm thử. Ba bài xUnit tích hợp cũ đang SKIP; kiểm tra browser chủ yếu là route, dữ liệu API, lỗi và bố cục, không phải thao tác mọi biểu mẫu. Các lần E2E chạy nối tiếp trên fixture TEST có trạng thái và giữ lịch sử; không gọi đây là một lần chạy mới toàn bộ 28 nhóm từ database trống.

## Kết quả theo yêu cầu §31

| Hạng mục | Kết quả và bằng chứng |
|---|---|
| PROJECT_TARGET | Project 2 Compact, single project ASP.NET Core; frontend/src/compact. Không thay đổi Project 1 trong P0. |
| DATABASE_TARGET | HotelOpsCompactWorkdayTestP0261124; schema 3.0.0, 49 bảng, 205 FK, 97 checks, 38 triggers. |
| BACKUP_STATUS | VERIFIED — COPY_ONLY/CHECKSUM, VERIFYONLY và restore baseline thật. Sao lưu TEST mới trước thêm fixture legacy; metadata artifacts/workday. |
| SOURCE_AUDIT | IMPLEMENTED — đối chiếu A/B/quyết định người dùng, giữ cấu trúc một backend; kiểm tra admission tại service và SQL. Worktree có thay đổi tồn tại trước P0, không reset/ghi đè. |
| DATABASE_MIGRATION | VERIFIED trên TEST — 9 script theo thứ tự, kiểm tra rollback/idempotency/catalog integrity. Chưa áp dụng primary. |
| ACTIVE_BUSINESS_ROLES | 8 actor: SYS_ADMIN, OWNER, OPERATION_MANAGER, HOUSEKEEPING_SUPERVISOR, HK_STAFF, MAINTENANCE_SUPERVISOR, RECEPTION, STOREKEEPER. E2E-01/27 và browser hai chi nhánh. |
| OM_DH_CONSOLIDATION | VERIFIED — chuyển DH sang OM theo phạm vi chi nhánh, thu hồi assignment cũ, giữ tài khoản/lịch sử. |
| MAINTENANCE_SUPERVISOR_MIGRATION | VERIFIED — Lead sang Supervisor; luồng giám sát sửa chữa bên ngoài, không tạo tài khoản kỹ thuật viên ngoài. |
| MAINTENANCE_STAFF_TO_HK_STAFF | VERIFIED — mapping role/membership/lịch phù hợp; không xóa user. |
| LEGACY_PERMISSION_REVOCATION | VERIFIED — 92 grant cũ thay thế có chủ đích; toàn bộ grant baseline còn trong migration audit snapshot. Session cũ bị thu hồi. |
| USER_COUNT_BEFORE_AFTER | VERIFIED — 57 user gốc còn nguyên, thêm 20 user TEST được duyệt = 77; 0 thay đổi credential gốc. preservation-audit.json. |
| ROLE_PERMISSION_MATRIX | IMPLEMENTED + API negatives VERIFIED — [matrix](permission-matrix.md); Owner read-only, Admin không tự có quyền nghiệp vụ, kiểm tra scope/ownership/handler. |
| WORKDAY_ALIGNMENT | VERIFIED — start/retry/end/retry, ngăn đóng khi còn việc/reservation/request; cho hoàn thành việc cũ sau giờ. Khôi phục legacy có endpoint riêng và audit, không sửa mốc lịch sử. |
| ITEM_CLASSIFICATION | VERIFIED theo fixture — A tiêu hao/B lâu bền/C thiết bị; chỉ đồ đã gán phòng cho báo thiếu/hỏng. |
| BRANCH_SUPPLY_CONFIGURATION | VERIFIED — 8 loại fill, định mức/low stock độc lập DVBV/DVCT, từ chối ngoài chi nhánh. |
| START_SHIFT_AUTO_ALLOCATION | VERIFIED — cấp đúng một lần, shortage không cấp một phần giả; E2E-03/04. |
| STAFF_SUPPLY_LEDGER | VERIFIED — issue/reserve/use/release/return có provenance; đối soát balances không âm. |
| CLEANING_SUPPLY_RESERVATION | VERIFIED — giữ chỗ theo sức chứa, assignment race, snapshot không thay theo config mới; E2E-05/07 và supplement report-snapshot. |
| CLEANING_ASSIGNMENT_VALIDATION | VERIFIED — thiếu vật tư bị từ chối, không để lại reservation; reassign/cancel giải phóng đúng; E2E-06/08. |
| CLEANING_SUPPLY_CONSUMPTION | VERIFIED — complete/retry không trừ hai lần; legacy sau giờ tiêu hao đúng sức chứa. |
| CONSUMABLE_REFILL_REQUEST | VERIFIED — tạo, từ chối, xác nhận, chuyển kho, cấp phát; E2E-09…12. |
| SUPERVISOR_APPROVAL | VERIFIED — đúng hàng đợi, lý do từ chối, không tạo stock khi duyệt; không cho generic API vượt quy trình. |
| STOREKEEPER_FULFILLMENT | VERIFIED — stock validation, ownership/current handler/workday, concurrent issue; OM không xử lý thay. |
| END_SHIFT_AUTO_RETURN | VERIFIED — trả đồ thừa trong transaction đóng ca, retry không trả trùng; E2E-13/14 và legacy supplement. |
| LOW_STOCK_ALERT | VERIFIED — dưới ngưỡng, không tạo trùng, giải quyết khi bổ sung; E2E-15. Worker integration cũ vẫn SKIP, không suy rộng kết quả đó. |
| WAREHOUSE_REPLENISHMENT | VERIFIED — Store request → OM approve → actual receipt mới tăng tồn; E2E-16. |
| DURABLE_ITEM_MISSING_REPORT | VERIFIED — chỉ đồ thuộc phòng, nhóm B, HK → supervisor; E2E-17…19. |
| DURABLE_ITEM_REMEDIATION | VERIFIED — kho thiếu không báo khắc phục, reinspection/rework giữ history và không xuất trùng. |
| EQUIPMENT_DAMAGE_REPORT | VERIFIED — HK/Reception báo, supervisor xác minh, occupancy không bị đổi trái phép; E2E-20/21/23. |
| EXTERNAL_MAINTENANCE_WORKFLOW | VERIFIED — Maintenance Supervisor bắt đầu/hoàn tất qua room workflow, generic maintenance action bị từ chối. |
| MAINTENANCE_REWORK | VERIFIED — kiểm tra không đạt có lý do, làm lại và kiểm tra tiếp; E2E-22. |
| ROOM_REINSPECTION | VERIFIED — child request có correlation/parent link; supervisor PASS/FAIL checklist giấy. |
| ROOM_READINESS_POLICY | VERIFIED — nhiều blocker, chỉ giải quyết đúng nguồn; không Ready khi còn blocker; E2E-24. |
| UNIFIED_INTERNAL_REQUEST | IMPLEMENTED + VERIFIED nhóm E2E-25 — inbox, loại/form theo role, queue, timeline, history, liên kết parent-child. |
| REQUEST_ROUTING | VERIFIED — actor/branch/status/current handler; stale handler sau khi hết ca có thể được người đang trực nhận lại, có audit/retry. |
| NOTIFICATION_SYSTEM | VERIFIED trong các scenario request/stock/room và retry được thực thi; không khẳng định mọi thời điểm worker nền đã được soak-test. |
| AUDIT_HISTORY | VERIFIED — lịch sử gốc không mất; event/provenance mới append; idempotency giữ số bản ghi ở các retry đã kiểm tra. |
| ROLE_BASED_UI | VERIFIED 100 lượt route/layout hai chi nhánh, 8 actor, 1440px/390px; thêm 6 kiểm tra Action Center/report có dữ liệu. Filter ẩn mặc định; sorting trên toàn bộ trang dữ liệu được phép đã tải. |
| TRANSACTION_INTEGRITY | VERIFIED trong shortage/rollback/issue/return/transfer/recovery scenarios; không vô hiệu hóa FK/check/trigger. Deadlock SQL bọc trong EF trả 409 để client thử lại, không 500. |
| CONCURRENCY_TESTS | VERIFIED — cạnh tranh tồn/reservation, approve-reject, retries start/complete/end/inspection, concurrent transfer receipt, configuration vs assignment. |
| INVENTORY_RECONCILIATION | VERIFIED — E2E-28 và supplements đối soát hai branch, staff balances; dữ liệu nhận hàng mô phỏng TEST qua API, không phải tồn thật khách sạn. |
| BACKEND_BUILD | PASSED — dotnet build HotelOps.slnx, 0 warning/0 error (build cuối chứa legacy initializer và asset recipient guard). |
| FRONTEND_BUILD | PASSED — npm run build (tsc + Vite); nhãn filter tiếng Việt được sửa và build lại. |
| UNIT_TESTS | PASSED 98; SKIPPED 3; tổng 101. Các skip: AccountManagement integration, MaintenanceWorker legacy Lead, Worker recurrence/low stock integration. |
| INTEGRATION_TESTS | VERIFIED theo các API/DB runtime artifacts và migration tests; 3 xUnit integration trên không được tính PASS. |
| E2E_TESTS | 28 nhóm đã VERIFIED cộng dồn, 1.349 assertions được chọn; 5 supplements và 8 nhóm transfer bổ sung. Không phải một clean-run duy nhất. |
| SRS_DOCUMENTATION | IMPLEMENTED — append bộ P0/traceability/contracts/matrix/recovery/evidence; SRS gốc giữ lịch sử. Phân biệt mốc audit cũ với kết quả hiện tại. |
| FILES_CHANGED | Theo module ở phần dưới; không quy mọi dirty file của repo cho lượt này. |
| RECOVERY_PLAN | [Database/recovery](database-and-recovery.md); restore vào TEST mới, không tự restore đè primary; không reset Git. |
| REMAINING_BLOCKERS | Không có quyết định người dùng đang chờ. Giới hạn kiểm chứng còn lại: 3 xUnit SKIP, browser không bao phủ thao tác mọi form, chưa soak/load test dài hạn và chưa clean-run 28 nhóm trên clone thứ hai. |
| FINAL_VERDICT | IMPLEMENTED trên TEST; VERIFIED cho các luồng được ghi bằng chứng. Chưa deploy/cutover Production. Không tuyên bố kiểm thử toàn bộ mọi tổ hợp hoặc thay thế UAT người dùng. |

## Phạm vi A còn hiệu lực

Điều chuyển liên cơ sở đạt 8 nhóm API: yêu cầu/duyệt, xuất, nhận từng phần, nhận đồng thời, hủy/từ chối, thiếu tồn và phân quyền hai bên. OM self-review chỉ OPERATIONAL; 33 assertions. Tài sản create/update/move/handover/accept/retire và Owner/Admin/foreign negatives: 36 assertions. Recipient phải còn quyền ASSET_VIEW trong chi nhánh và không là Owner/Admin; không cho offer đến người không thể nhận.

Action Center dùng 8 nhóm từ server và link hồ sơ thật. Bàn giao tái sử dụng backlog công việc đang có; không tạo thực thể bàn giao ca mới hoặc cho HK bàn giao công việc đã bắt đầu. Báo cáo transfer và stocktake-differences có kiểm chứng API, chứng từ có dòng thật, scope hai branch và browser mobile. Kiểm kê TEST chỉ ghi chênh lệch mô phỏng, chưa điều chỉnh tồn.

## Các lỗi đã xử lý trong integration

- Route action dùng token MVC gây 404; đổi route parameter sang command.
- Guard chứng từ room-remediation không khớp root/child; sửa kiểm tra nguồn và giai đoạn.
- Request child INTERNAL_REQUEST không được đặt WorkItem.ParentWorkId do constraint; giữ quan hệ tại RequestWorkflow.ParentRequestWorkId.
- Chặn gọi trực tiếp generic maintenance để bỏ qua room workflow.
- SQL deadlock lồng DbUpdateException được nhận diện, trả 409 có mã nghiệp vụ.
- Người đã hết ca giữ handler khiến yêu cầu mắc kẹt: thêm nhận lại đúng queue, audit và idempotency.
- Legacy cleaning thiếu allocation/reservation: phục hồi rõ ràng trong transaction, cấp hiện tại từ kho thật, không suy diễn tiêu hao lịch sử.
- Mobile overflow và màu/nhãn dashboard; sửa lỗi dấu tiếng Việt trong bộ lọc yêu cầu.
- Script bổ sung tự import ở top-level await gây treo; runner nhận adapter object. Assertion config-race ban đầu dùng field DTO không tồn tại đã sửa; kết quả thất bại cũ không tính vào PASS.

## Files theo module

- Backend Compact: RequestWorkflowService và các partial, StaffSupplyService và các partial, WorkService/RoomIssue partials, WorkdayService, DocumentService, AssetService, InsightService, Access/AuthService, ApiSupport, SchemaGate, controller/DTO/entities/mappings P0.
- Frontend Compact: requests, staff-supplies, supply-configuration, operations-action-center, cleaning-supply-reconciliation, assets, work/workday, navigation/role routing, reports và CSS mobile.
- Database: p0-role-migration, p0-supplies, p0-store-policy, p0-readiness, p0-workflow-guards, p0-requests, p0-room-request-backfill, p0-supply-defaults, p0-finalize; TEST runner/backup.
- Kiểm thử mới/bổ sung lượt tiếp tục: p0-runtime-legacy.mjs, p0-legacy-fixture.ps1, p0-runtime-report-snapshot.mjs, p0-evidence-summary.mjs; sửa p0-runtime-runner/handler-scenarios; chạy lại transfer/browser/asset/preservation.
- Tài liệu: bộ docs/requirements/p0-261124, SRS_CHANGE_NOTES, kế hoạch plans/261124-1237-p0-workflow.

## Môi trường còn chạy

Development TEST: frontend http://127.0.0.1:5174 (PID 48136), API http://127.0.0.1:5774 (PID 15136). Production frontend PID 30516 và API PID 40444 được giữ nguyên. Các build/test hữu hạn đã kết thúc; không tạo thêm sub-agent.
