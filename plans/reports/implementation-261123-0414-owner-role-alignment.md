# Bàn giao Owner — Project 2 / CompactDB

Đã triển khai workspace giám sát bằng API/SQL thật, có tổng hợp ALL, lọc từng chi nhánh, so sánh, báo cáo và drill-down chỉ đọc. Giữ UX compact đã chốt. Không sửa grants, schema, generated entities, Staffing V3/V4, tài khoản hoặc Project 1; không commit/push/deploy.

## Kết quả theo từng yêu cầu

| Hạng mục | Trạng thái | Bằng chứng / giới hạn |
|---|---|---|
| OWNER_ROLE | VERIFIED | SQL chỉ đọc: OWNER role 12, đang hoạt động; không hardcode role ID trong nghiệp vụ |
| OWNER_USER_ID | VERIFIED | User 438 / OWN001 ACTIVE; assignment branch_id NULL. Một membership đã có sẵn được giữ nguyên, không thêm hoặc sửa. Test actor không có membership vẫn đọc được cả hai nhánh |
| OWNER_PERMISSION_COUNT | VERIFIED | Đúng 12 grants, đối chiếu lại sau triển khai; artifact primary-role-audit.json. Không thêm/bớt quyền |
| OWNER_SCOPE | VERIFIED | ALL cho từng grant thực có; bỏ branchId đòi global grant của domain đó, branchId hợp lệ chỉ là bộ lọc |
| SOURCE_AUDIT | VERIFIED | Bảng FEATURE/CURRENT_UI/API/PERMISSION/SCOPE/GAP/ACTION trong plan; đọc Change Notes, Owner/SRS draft, DbContext, entities, views/guards, Access/Auth, Work/Room/Asset/Inventory/Reports/Workday và UI. Ưu tiên yêu cầu hiện tại và baseline thực tế |
| SIDEBAR_ALIGNMENT | IMPLEMENTED / VERIFIED | 7 nhóm có quyền: tổng quan, chi nhánh, phòng, công việc, sự cố/bảo trì, tài sản/kho, báo cáo. Nhóm audit thứ tám ẩn vì thiếu grant. Lucide, responsive, từng menu/tab theo permission; notifications qua header |
| GLOBAL_DASHBOARD | IMPLEMENTED / VERIFIED | KPI backend theo domain được đọc, trạng thái hiện tại/updatedAt, flow trong kỳ, room/asset breakdown, attention/critical lists, biểu đồ ngày/tuần/tháng. Không hardcode dữ liệu hay KPI tài chính |
| BRANCH_COMPARISON | IMPLEMENTED / VERIFIED | Cards, biểu đồ, bảng và drawer; tổng phòng/công việc bằng tổng nhánh; shared item toàn hệ thống đếm DISTINCT thay vì cộng trùng số mặt hàng |
| BRANCH_SELECTOR | IMPLEMENTED / VERIFIED | Tất cả / từng nhánh dưới logo; chuyển scope refresh dữ liệu và reset scroll. Không đổi role, grants hoặc membership; fixture chọn A/B/ALL xác minh đúng số hàng |
| ROOM_MONITORING | IMPLEMENTED / VERIFIED | Grid 15 ô/trang, tìm/tầng/trạng thái, type/capacity/branch/floor, drawer/history/Incident links. Tách occupancy, cleaning, inspection và operational block; block tương lai không tính đang hiệu lực. Check-in/out/move/block/unblock bị 403 |
| HOUSEKEEPING_MONITORING | IMPLEMENTED / VERIFIED | Cleaning/Inspection/Rework dùng Work hiện có, search/filter/sort/page, người thực hiện, trạng thái và lịch sử; không assign/execute/inspect/verify/reopen |
| MAINTENANCE_MONITORING | IMPLEMENTED / VERIFIED | Danh sách/drawer, metadata người phụ trách/thiết bị/ưu tiên, completion note, review/history. COMPLETED hiển thị Chờ nghiệm thu; không tự đóng Incident hoặc thay trạng thái phòng/tài sản |
| INCIDENT_MONITORING | IMPLEMENTED / VERIFIED | Đúng branch, severity, DueAt/SLA, trạng thái và liên kết. Tách unresolved với unclosed: RESOLVED vẫn chờ closure; SLA chưa đóng theo contract hiện hành, không tự sửa deadline/triage/escalate/resolve |
| OPERATIONAL_TASK_MONITORING | IMPLEMENTED / VERIFIED | TASK_VIEW và IR_VIEW thực có; các lifecycle riêng, xem tiến độ/assignee/history; không tạo, giao, thực hiện hoặc đóng công việc |
| ASSET_MONITORING | IMPLEMENTED / VERIFIED | Danh sách đa chi nhánh, tìm/lọc/sort/page, drawer vị trí/sửa chữa/sự cố/history và liên kết PM đọc. Không create/edit/move/condition/retire/handover, kể cả lời mời cũ gửi cho Owner |
| INVENTORY_MONITORING | IMPLEMENTED / VERIFIED | Số lượng ledger theo đơn vị gốc, warehouse/item/branch, policy, lịch sử/chứng từ. Không dựng reserved/available hay low-stock khi thiếu policy; không nhập/xuất/điều chỉnh/chuyển/kiểm kê |
| MATERIAL_REQUEST_VISIBILITY | VERIFIED | MR_VIEW ALL thực có; list/detail/history/fulfillment đọc được cả hai nhánh. Không create/approve/reject/submit/cancel/close/fulfill. Không hiển thị link Replenishment ngoài quyền đọc |
| REPORTING | IMPLEMENTED / VERIFIED / PARTIAL | Overview, rooms, cleaning, inspection, maintenance, incidents/SLA, tasks, IR, MR, assets, stock, documents, PM, staffing. Tìm/sort/page, khoảng ngày và presets, so sánh, đọc chi tiết. CSV bảng so sánh xuất toàn phạm vi; CSV danh sách chỉ trang hiện tại, ghi rõ trên nút. Không có full-snapshot export nhiều trang hay dữ liệu tài chính giả |
| AUDIT_VIEW | BLOCKED | Owner không có AUDIT_VIEW trong 12 grant. Menu ẩn, direct API trả 403; lịch sử nghiệp vụ vẫn đọc theo domain permission. Không cấp thêm quyền |
| STAFFING_REPORT_VISIBILITY | IMPLEMENTED / VERIFIED | REPORT_VIEW cung cấp số nhân viên theo bộ phận, assigned staff/slots, capacity, underfilled shifts, tỷ lệ kèm mẫu số, completed work. Tìm/sort/page; không trả hồ sơ cá nhân/email và không gọi User Management/Workday management API |
| BACKEND_AUTHORIZATION | IMPLEMENTED / VERIFIED | Mỗi API đọc kiểm tra permission/domain/scope. Không dùng role-code hard check thay authorization. Work/Document global lists yêu cầu ALL khi thiếu branch; quyền đọc không cho mutation |
| ALL_SCOPE_ENFORCEMENT | VERIFIED | Actor 12 ALL grants, assignment NULL và 0 membership đọc được A/B/ALL. Mixed reader DASHBOARD/REPORT ALL + ROOM_VIEW BRANCH không thể lấy rooms/report toàn hệ thống. Branch-only reader thiếu branchId bị 403 |
| READ_ONLY_ENFORCEMENT | IMPLEMENTED / VERIFIED | Mutation đúng DTO bị 403; SQL counters work/room/asset/stock không thay đổi do Owner. Revalidate execution khi quyết định handover; Asset global-view-only không đủ acknowledgment. Browser 0 non-GET business calls; CSV tạo file cục bộ |
| FRONTEND_BUILD | VERIFIED | Typecheck và npm run build passed, 1637 modules; targeted formatting passed; không warning build |
| BACKEND_BUILD | VERIFIED | dotnet build HotelOps.slnx: 0 warnings, 0 errors |
| TEST_RESULTS | VERIFIED / NOT_TESTED | 33 nhóm Owner API/RBAC/scope/read-only + 45 real-API responsive views; 128 regression API; .NET 28 pass / 3 conditional skip; FE workflow 5 + CSV 2 pass. Chi tiết bên dưới. Không kiểm thử ghi nghiệp vụ primary, load test, toàn bộ browser engines hoặc email integration |
| FILES_CHANGED | IMPLEMENTED | Danh sách bên dưới, tách khỏi nhiều thay đổi có sẵn/untracked của repository |
| REMAINING_BLOCKERS | BLOCKED / PARTIAL | Audit bị chặn bởi grants. Snapshot phòng/asset/stock/PM là hiện tại; không có API tái dựng snapshot quá khứ hoặc export snapshot ổn định qua nhiều trang, nên không giả lập. CSV danh sách giới hạn đúng trang hiển thị |
| FINAL_VERDICT | IMPLEMENTED / VERIFIED trong 12 grants; PARTIAL với audit/export lịch sử mở rộng | All branches → dashboard → comparison → monitoring → reports → detail hoạt động trên fixture API thật; quyền primary giữ nguyên. Không tuyên bố audit đã triển khai hoặc login/mutation primary đã được kiểm thử |

## Baseline đã bảo toàn

Tất cả scope ALL: ASSET_VIEW, CLEANING_VIEW, DASHBOARD_VIEW, INCIDENT_VIEW, INSPECTION_VIEW, INVENTORY_VIEW, IR_VIEW, MAINTENANCE_VIEW, MR_VIEW, REPORT_VIEW, ROOM_VIEW, TASK_VIEW.

Không có AUDIT_VIEW, PM_MANAGE, REPLENISHMENT_VIEW, USER_VIEW, USER_MANAGE, SHIFT_MANAGE, ATTENDANCE_RECORD hoặc quyền ghi nghiệp vụ. Role/user/assignment/grants/membership primary không thay đổi. Artifact `artifacts/owner/primary-role-audit.json` chỉ chứa metadata đối chiếu, không chứa credentials.

## Kiểm chứng thực tế

- `Test-Backend.ps1 -Owner -Port 5187`: **33/33** nhóm assertions, database cuối `HotelOpsCompactApiTest7a3ea7e748e0`, backup được xác minh trước fixture writes. Các nhóm bao phủ nhiều HTTP/SQL assertions, không gọi đây là chỉ 33 request. JSON: `artifacts/compact-v2/backend/owner-verification.json`.
- `frontend/tests/compact-owner-live.mjs`: **45 views** tại 1440/900/375, selector A/B/ALL, report paging/search, branch/work/room/asset/PM/MR drawers, download CSV thật. **0 mutations, 0 page/console errors, 0 failed permitted API calls**. API proxy chuyển sang fixture, không gọi mutation primary. JSON/ảnh/filtered-report.csv trong `artifacts/owner/` là dữ liệu fixture, không phải dữ liệu kinh doanh thật để phát hành.
- `Test-Backend.ps1 -Extended -Port 5188`: **128/128**, fixture `HotelOpsCompactApiTest750acac0528f`; bao gồm positive asset handover và work handover, occupancy/inspection/rework, stock/MR/replenishment, reports/audit và authorization. 40 tables/160 FKs giữ nguyên.
- `dotnet test HotelOps.slnx --no-build`: **28 passed / 3 skipped**. Skipped: MaintenanceWorker, recurrence/low-stock Worker, account/email integration cần fixture/env riêng. Không tính ba bài skip là PASS của lượt này.
- `node --test tests/compact-report-csv.test.mjs tests/compact-workflow.test.mjs`: **7/7**. CSV kiểm tra tiếng Việt, dấu phẩy/quote/newline, sparse columns và formula injection, giữ số lượng âm là số. Năm workflow tests giữ nhận việc/execute/review/IR separation.
- Shared UX: navbar **48 route/viewport**, settings/Reception/Workday/dashboard suites. Owner mock dashboard được cập nhật đúng 12 ALL grants/API mới, không giữ fixture BRANCH lỗi thời. Regression mock HK Staff **27**, OM **24**, Department Head **21**, Storekeeper **27** responsive views đều pass. Mock UI không được dùng làm bằng chứng SQL.
- Các lỗi gặp và đã sửa: nullable occupancy mapping ở build đầu; fixture thiếu parent/checklist/turnover hợp lệ; OUTPUT không phù hợp table có trigger; handover fixture thiếu context; browser teardown khi request bị hủy. Giữ SQL guards, không nới validation để test pass. Browser test đặt timeout rõ ràng, chờ dữ liệu/network, xử lý cancellation khi điều hướng và cleanup routes/context; fixture sessions được thu hồi khi kết thúc. Không lưu token trong JSON/CSV/ảnh.
- Không chạy toàn bộ npm suites hoặc load test; không dùng actual primary login để test vì sẽ tạo session/cập nhật last-login. Primary chỉ audit SQL đọc và health check; toàn bộ nghiệp vụ/negative mutations kiểm chứng trên fixture.

## Hợp đồng và giới hạn dữ liệu

Dashboard snapshot và báo cáo snapshot là trạng thái hiện tại. Khoảng ngày dashboard chỉ áp dụng cho số đối tượng tạo/hoàn thành; hoàn thành theo mốc gửi kết quả gần nhất, không đồng nghĩa đã nghiệm thu. Báo cáo danh sách công việc/chứng từ là cohort theo CreatedAt; UI nói rõ và vẫn hiển thị CompletedAt/VerifiedAt riêng. Lịch nhân sự dùng WorkDate trong kỳ và completed task theo CompletedAt. Ngày báo cáo hiển thị Asia/Ho_Chi_Minh; biểu đồ dashboard gom theo timezone từng nhánh. Timestamp SQL/API dùng UTC, CSV giữ giá trị nguồn.

Tổng số mặt hàng dùng DISTINCT item_id toàn phạm vi; low-stock/empty stock là số cặp kho-vật tư đã theo dõi, không cộng các lượng vật tư khác đơn vị thành một tổng giả. Dữ liệu reserved/available không có trong contract nên không tạo giá trị 0 giả. Lịch sử chi tiết giữ giới hạn API hiện hữu, không trình bày như audit toàn hệ thống.

## Các file tạo/sửa trong lượt Owner

Backend:

- Tạo `src/HotelOps.Api/Compact/Services/InsightService.Owner.cs`.
- Tạo `src/HotelOps.Api/Compact/Services/InsightService.OwnerReports.cs`.
- Sửa `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`.
- Sửa `src/HotelOps.Api/Compact/Services/WorkService.cs`.
- Sửa `src/HotelOps.Api/Compact/Services/WorkService.Maintenance.cs`.
- Sửa `src/HotelOps.Api/Compact/Services/DocumentService.cs`.
- Sửa `src/HotelOps.Api/Compact/Services/AssetService.cs`.

Frontend:

- Tạo `frontend/src/compact/owner-overview.tsx`, `owner-reports.tsx`, `owner-workspace.tsx`, `report-csv.ts`.
- Sửa `frontend/src/compact/app.tsx`, `rooms.tsx`, `work.tsx`, `assets.tsx`, `stock.tsx`, `documents.tsx`, `insights.tsx`.

Tests:

- Tạo `scripts/database/compact-v2/owner-fixture.sql` và `test-owner.ps1`.
- Sửa `scripts/database/compact-v2/Test-Backend.ps1` để thêm nhánh Owner và thu hồi sessions fixture.
- Tạo `frontend/tests/compact-owner-live.mjs`, `compact-report-csv.test.mjs`.
- Sửa `frontend/tests/compact-dashboard-pagination.mjs` theo contract Owner mới.

Docs:

- `docs/ui/compact-ux.md` — mục Owner.
- `plans/261123-0414-owner-role-alignment/plan.md` — scope/audit/evidence.
- Báo cáo này và `plans/journals/2026-11-23-owner-role-alignment.md` (CLI validate passed; AgentWiki publish skipped).

Nhiều thư mục Project 2 đã untracked từ trước; danh sách này chỉ là các file sửa/tạo cho lượt Owner. Không stage/reset, không ghi đè dirty baseline và không tạo/chuyển branch.

`git diff --check` passed; Git vẫn cảnh báo LF → CRLF ở ba file baseline có sẵn (`frontend/package-lock.json`, `frontend/package.json`, `frontend/src/main.tsx`). Không có lỗi whitespace hoặc warning build mới. Không tự đổi line endings của baseline.

## Môi trường bàn giao

API chính Project 2 đã được khôi phục ở 127.0.0.1:5768, HotelOpsCompactDB, worker=false. Vite 5186 giữ nguyên. Fixture API/browser được đóng; database fixture/backup/verification artifacts giữ để chẩn đoán, không tự xóa. Không tác động process Demo/Project 1 hoặc triển khai lên môi trường khác.
