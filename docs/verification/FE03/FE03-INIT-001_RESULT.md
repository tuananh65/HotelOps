# FE03-INIT-001 RESULT

## Trạng thái bàn giao

- BASELINE_SHA / HEAD_SHA: bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c
- Worktree: D:\SEP490\Projects\HotelOps-FE03
- Branch: feature/dev1-fe03-uc027-implementation
- CONTRACT_STATUS: PARTIAL. Effective Readiness và mutation: BLOCKED_CONTRACT.
- Đã triển khai increment read-only backend và frontend. Chưa LIVE_VERIFIED / PR_MERGED / TESTER_READY / ACCEPTED cho toàn bộ UC027.
- COMMIT_SHA: Không tạo commit; thay đổi chưa commit.
- TASK_COMPLETION_PERCENT: CHƯA ĐỦ DỮ LIỆU.
- UC027_VERIFIED_PERCENT: CHƯA ĐỦ DỮ LIỆU. Chưa có denominator contract đầy đủ hoặc isolated SQL/HTTPS evidence.

## Contract và reuse có giới hạn

Project Leader xác nhận UC DETAIL, UC027 — Manage Operational Room Status là working scope; workbook không phải bằng chứng toàn bộ contract đã duyệt:
https://docs.google.com/spreadsheets/d/1k_N0SF_Np-stuxECS_ESUYZldsAo_DjX3KdUTfh1xsM
Công cụ web không đọc được cả URL workbook và UC DETAIL CSV view. Mâu thuẫn mã giữa các sheet được Project Leader thông báo; chưa đối chiếu độc lập nội dung workbook.

Nguồn local đã đối chiếu: docs/requirements/SRS_CHANGE_NOTES.md; HotelOps_SRS_DRAFT.md phần UC027 / Room Readiness / GBR-043 và GBR-044; entities và HotelOpsDbContext liên quan.
UC011 local là Review User Access Permissions thuộc FE01, không phải room monitoring.
Historical actors UC027: Housekeeping Supervisor, Operations Manager. Reception permissions chưa duyệt. Không suy ra grants từ actors hay role IDs.
Stored operational filter codes hiện có: AVAILABLE, OCCUPIED, DIRTY, CLEANING, INSPECTION, READY, OUT_OF_SERVICE. Không map sang readiness labels.
Project Leader xác nhận chưa có authoritative Effective Readiness source và chưa phê duyệt quyền cập nhật bổ sung. Không triển khai transition pending, status override hoặc competing room-status writer.

| Capability | Phân loại | Source / giới hạn |
| --- | --- | --- |
| FE02 Room/Branch/Floor/Area | EXISTING_REUSABLE | Entities, DbContext, RoomTypeSchema.RoomsAsync; directory giữ same-branch floor/area/type checks. |
| Room list/detail/history + authorization | EXISTING_REUSABLE | RoomsController, RoomDirectoryService, ROOM_VIEW branch/global checks, SQL session revalidation, serializable transaction, DTOs, cancellation. |
| Operational counters | MISSING -> IMPLEMENTED | Đếm trên scoped/filtered rooms trước pagination; exact persisted codes. |
| RoomBlock detail | PARTIAL -> EXTENDED | Chỉ đọc same-room unreleased records sau khi xác minh room authorization; không expose Incident ID/detail. Unreleased không đồng nghĩa effective readiness. |
| FE04 cleaning lifecycle | EXISTING_REUSABLE | Creation/lifecycle/closure/cancellation và persisted cycle/readiness events hiện có; không sao chép hoặc đổi writer. |
| FE05 inspection/projection | PARTIAL / BLOCKED_CONTRACT | InspectionExecutionService tồn tại; TurnoverCycleReadService đánh dấu inspection NOT_ACCEPTED và readiness PERSISTED_EVENTS_NOT_SHARED_ACCEPTED. Không tìm thấy accepted dedicated RoomReadiness/RoomProjection service trong baseline. |
| FE06 integration | PARTIAL | RoomBlock schema/source có sẵn; FE03 chỉ đọc recorded unreleased blocks. Không đổi IncidentService/release policy/shared SQL. |
| FE03 frontend | PARTIAL -> READ BOARD IMPLEMENTED | Reuse listRooms, cookie transport, router, styles/table/filter components. Board/detail/history dùng API; readiness và downstream actions còn thiếu accepted contract. |
| Mutation | BLOCKED_CONTRACT | Thiếu approved transitions, actor permissions và readiness/block policy; chưa triển khai mutation/concurrency/audit workflow. |

## Backend đã triển khai

- GET /api/rooms: additive statusCounts [{ operationalStatusCode, count }] cho toàn bộ tập sau bộ lọc, trước phân trang. Áp dụng cả status filter; không chỉ đếm trang hiện tại. Empty matches: [].
- GET /api/rooms/{roomId}?branchId=...: additive unreleasedBlocks [{ id, reason, blockedAtUtc, expectedReleaseAtUtc }]. Không tự coi expected release là actual release; không derive readiness.
- Giữ existing routes, cookie authentication, HTTPS requirement, ROOM_VIEW checks và HTTP error mappings. Không thêm mutation hoặc workflow 409.
- AsNoTracking, async query và CancellationToken trong transaction đọc hiện có, trước session/permission recheck cuối.

## Frontend đã triển khai

- Route /rooms/status; thêm navigation Room Status vào composition hiện có, giữ nguyên các FE04 routes.
- Board nhóm theo tầng trên trang hiện tại; bộ đếm lấy toàn bộ tập matching từ API.
- Bộ lọc branch/floor/area/room type/status/room number; pagination; URL giữ bộ lọc khi mở phòng và quay lại.
- Detail: recorded operational status, current evidence, unreleased RoomBlocks; recorded status history có pagination.
- Effective Readiness hiển thị chưa có nguồn được xác nhận; không suy ra từ Cleaning/Inspection, không có nút mutation chưa được phép.
- Reuse cookie GET transport; validate IDs, page offset, branch/room identity, timestamps, history coverage và aggregate counts. Không chuyển actor/grants/allBranches không tin cậy vào requests.
- Abort request khi đổi scope; key response theo query/retry, ẩn data cũ khi đổi branch hoặc bị từ chối. Loading/error/empty/retry; xử lý 400/401/403/404/409/503. Không fallback mock trong production source.
- Browser fixtures chỉ nằm trong tests; không phải authoritative production data.

## Validation

- Backend dotnet build HotelOps.slnx: PASS, 0 warnings, 0 errors trên trạng thái bàn giao cuối (--no-restore).
- dotnet test HotelOps.slnx: exit 0; solution chỉ chứa API project nên không thực thi regression test project. Đã chạy test project riêng.
- Backend focused/regression chạy lại cuối: PASS 63, SKIP 1 SQL opt-in, FAIL 0. Riêng RoomOperationalReadTests: PASS 10/10.
- FE04 focused regressions (CleaningLifecycleContractTests, CleaningClosureBoundaryTests, CleaningMonitorFs016Tests, CleaningMonitorRouteTests): PASS 53, SKIP 1 SQL opt-in, FAIL 0.
- Initial LINQ translation test phát hiện lỗi sort sau DTO projection; đã sửa sort trước projection và focused tests PASS.
- npm run typecheck: PASS.
- npm run build: PASS. Hai warnings từ React Router: bỏ qua module directive use client khi bundle.
- npm test: PASS 55/55, gồm 6 FE03 API integration/validation tests và các FE04 client regressions.
- npx playwright test --config playwright.fe03.config.ts: PASS 10/10, Edge với intercepted HTTP fixtures. Kiểm tra filters/pagination/detail/history; 400/401/403/409/503; empty state; stale canceled response; ẩn data khi chuyển sang denied branch; reject response sai room.
- Backend tests xác minh LINQ SQL Server translation qua ToQueryString mà không kết nối DB; branch/filter isolation trong query tests và invalid actor short circuit. Không thay thế live authorization tests.
- SQL_HTTPS_STATUS: NOT RUN. Chưa verify actual query execution, real SQL permission revocation hoặc cross-branch HTTP isolation. Không có SQL/HTTPS PASS claim.
- npm ci giữ package-lock, có deprecation warnings cho inflight/lodash.isequal/rimraf/glob/fstream/uuid và blocked install scripts core-js/esbuild. Build/tests chạy thành công; không đổi dependencies để xử lý cảnh báo ngoài scope.
- Playwright có warning NO_COLOR bị FORCE_COLOR override. Không có test failure.

## Files changed (13)

Modified:
- src/HotelOps.Api/DTOs/Rooms/RoomDirectoryContracts.cs
- src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs
- frontend/src/api/dev1/rooms.ts
- frontend/src/main.tsx (route/navigation FE03 tối thiểu)

Created:
- src/HotelOps.Api/Services/Rooms/RoomOperationalReadQueries.cs
- tests/HotelOps.Dev1.RegressionTests/RoomOperationalReadTests.cs
- frontend/src/api/dev1/roomBoard.ts
- frontend/src/screens/rooms/RoomStatusBoardScreen.tsx
- frontend/tests/room-status-fixtures.mjs
- frontend/tests/room-status.test.mjs
- frontend/tests/room-status.spec.ts
- frontend/playwright.fe03.config.ts
- docs/verification/FE03/FE03-INIT-001_RESULT.md

Không sửa Program.cs, authentication, EF entities/mappings, shared SQL/schema, worktree khác; không merge/deploy/commit/push/PR.

## Blockers và next coding task

- BLOCKED_CONTRACT: authoritative Effective Readiness source và accepted FE05 projection; final status transitions, Reception/Operations Manager mutation permissions và block policy.
- Chưa có actor trong status history response hiện hành; chưa bổ sung actor read contract trong increment này. Cleaning/current-cycle details, accepted inspection/rework evidence và context-sensitive downstream actions chưa hoàn thiện.
- UC v3 chỉ là working reference theo Project Leader, nội dung sheet chưa đọc được qua web tool.
- NEXT_CODING_TASK: isolated read-only SQL/HTTPS trial cho list/detail/history và branch/room authorization bằng môi trường/fixtures được duyệt; tích hợp accepted RoomProjection/Readiness khi có contract; sau đó hoàn thiện nguồn cleaning/inspection/rework và downstream authorized actions. Mutation tiếp tục BLOCKED_CONTRACT.
