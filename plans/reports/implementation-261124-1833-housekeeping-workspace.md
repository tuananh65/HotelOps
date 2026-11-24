# Workspace vệ sinh — 24/11/2026

## Phạm vi và kết quả

IMPLEMENTED / VERIFIED trên Project 2. Không dùng sub-agent; không commit/push; giữ nguyên các thay đổi của lượt trước.

1. Nhân viên có hai tab Công việc hiện tại/Lịch sử vệ sinh. Việc dọn phòng và dọn lại được gộp trong danh sách hiện tại; công việc terminal không xuất hiện ở đây. Lịch sử lọc ngày/tuần theo giờ chi nhánh, tìm tên việc/phòng/người làm, trạng thái, sắp xếp và phân trang.
2. Theo xác nhận của người dùng, Giám sát buồng phòng không có trang Tình trạng phòng. Menu và route trực tiếp bị chặn; đường grant ROOM_VIEW của vai trò này không được dùng trong Access và auth/me. Những đường quyền của vai trò khác giữ nguyên. Không sửa schema hay xóa permission definitions/grants trong database. Endpoint phòng cần tạo việc dùng CLEANING_ASSIGN + CLEANING_VIEW, không đòi ROOM_VIEW.
3. Thời lượng dùng ba radio 20/25/30 phút. Backend từ chối giá trị khác khi tạo/phân công; hạn tính từ lúc giao việc.
4. Nút Báo sự cố màu cam, do capability backend quyết định; chỉ hiện lúc nhân viên đang dọn hoặc giám sát đang kiểm tra. API tải đồ vật và gửi báo cáo kiểm tra lại work/room/branch/turnover/assignment/status. Lễ tân vẫn giữ điều kiện ca trực từ lượt trước.
5. Tạo dọn lại chọn nhân viên đang trong ca; kế thừa phòng/bộ phận của việc gốc. Tạo và phân công trong cùng transaction. Đóng modal quay lại chi tiết gốc, không chuyển workspace sang trang xử lý làm lại.
6. Giám sát có hai tab Công việc hiện tại/Lịch sử vệ sinh. Các đường dẫn chi tiết cũ vẫn mở được.
7. Hàng đợi Tất cả đầu tiên, thêm Đã giao giữa Chưa phân công và Đang dọn. API assigned/doing tách biệt. Hàng đợi ở trên khung tìm kiếm và sắp xếp, không còn nút “Đang xem trong phạm vi quyền”.
8. Chi tiết vệ sinh bỏ Hủy công việc và Minh chứng; người giao/nhận/giờ nhận rõ ràng; không hiển thị ACTIVE. Báo sự cố cùng hàng với Xác nhận hoàn thành, trạng thái có khoảng cách cân đối.

## Bằng chứng

- `dotnet build HotelOps.slnx --no-restore`: PASSED, 0 warning, 0 error.
- `dotnet test HotelOps.slnx --no-restore` và kiểm tra lại bản build cuối bằng `--no-build --no-restore`: 101 passed, 3 skipped, 0 failed. Ba test có sẵn được cấu hình SKIP (worker, maintenance worker, account/email integration); không tính là pass.
- Frontend `npm run build`: PASSED. `npm run test:compact:ci`: 9 passed.
- Browser `frontend/tests/housekeeping-workspace-browser.mjs`: hai vai trò × 1440/390 px trên Develop; kiểm tra tab, route phòng trực tiếp, đóng dọn lại, radio thời lượng, điều kiện/màu báo sự cố, người nhận, bỏ hủy/minh chứng, lịch sử tuần, không tràn ngang và không pageerror. Đây là kiểm thử API fixture; không coi fixture là dữ liệu thật.
- API thật `scripts/verification/housekeeping-workspace-runtime.mjs` trên database TEST: ROOM_VIEW bị từ chối, phòng cần giao việc vẫn đọc được; bộ lọc hàng đợi; lịch sử tuần bằng tổng ngày; status/search/sort; cá nhân và chéo chi nhánh; báo sự cố không có work context bị chặn.
- API thật `scripts/verification/housekeeping-rework-runtime.mjs`: cả DVBV/DVCT TEST chạy tạo cleaning 20 phút → start → complete → FAIL → rework 25 phút đã phân công → start → complete → PASS. Xác nhận giờ nhận, hạn 25 phút, từ chối duration=15, nhân viên ngoài chi nhánh, trùng rework. Công việc TEST 752/754 và 756/758 kết thúc COMPLETED.
- Artefact: `artifacts/housekeeping-workspace/{browser,runtime,rework-runtime}.json` và ảnh desktop/mobile. Đã xem trực tiếp ảnh chi tiết mobile, lịch sử desktop và hàng đợi desktop.

## Lỗi được tìm và sửa trong kiểm chứng

- TypeScript phát hiện điều kiện CLEANING không thể xảy ra trong nhánh INSPECTION sau refactor; đã sửa, build sau đạt.
- Grant CLEANING_VIEW của tài khoản HK TEST có scope BRANCH: API lịch sử có thể bị gọi với mine=false. Đã ép lịch sử HK_STAFF theo actor của sự kiện COMPLETE ở backend; kiểm thử lại đạt (DVCT 7 việc toàn chi nhánh, nhân viên chỉ thấy 6 việc của mình).
- Một lần chạy test ngay sau restart bị ECONNREFUSED do API chưa sẵn sàng; kiểm tra health rồi chạy lại đạt.
- Publish Release cảnh báo MSB3026 một lần mỗi thư mục publish vì OFL.txt đang bị khóa; retry tự thành công, không có lỗi publish cuối cùng. Vite cảnh báo outDir ngoài frontend root là dự kiến, không xóa thư mục artifact.

## Dữ liệu / môi trường

- Không sửa schema, entity, mật khẩu hoặc dữ liệu nghiệp vụ Production. TEST chỉ dùng tài khoản/phòng P0 TEST có sẵn và thao tác nghiệp vụ qua API; giữ lịch sử.
- Backup TEST đã VERIFYONLY: `HotelOpsCompactWorkdayTestWarehouse261124_before_workday_20261124_185614643.bak`.
- Backup Production đã VERIFYONLY: `HotelOpsCompactDB_before_workday_20261124_190511024.bak`.
- Manifest backup: `artifacts/workday/backup-<database>.json`.
- Gói API: `artifacts/production-261124-housekeeping-workspace/api-final/HotelOps.Api.dll`, SHA256 `873E123542E0533EBB55DEA91348E71BD9EFC6D0C7EA16693C1674F40FBE8301`.
- Production API PID49196:5769, DB HotelOpsCompactDB. Develop API PID3876:5774, DB HotelOpsCompactWorkdayTestP0261124. Frontend Production 5080 và Develop 5174 giữ tiến trình cũ.
- Frontend đang phục vụ từ `artifacts/production-261124-p0/frontend`, bản trước được lưu tại `artifacts/production-261124-housekeeping-workspace/frontend-before`.
- Sau cập nhật: Production/Develop health `ok`, schema 3.0.0, 49 tables; frontend Production HTTP200, API chưa đăng nhập HTTP401. Browser fixture cũng chạy lại trên origin Production 5080, cả hai vai trò desktop/mobile đạt, không pageerror. API TEST tạm 5775 đã dừng.
- Khôi phục code: dùng gói API `production-261124-workday-role/api` và frontend-before; kiểm tra PID/cổng trước khi thay dịch vụ. Không cần rollback schema/data vì không migration Production.

## File của lượt này

Frontend: `api.ts`, `app.tsx`, `work.tsx`, `cleaning-queue.tsx`, `housekeeping-workspace.tsx`, `housekeeping-history.tsx`, `room-issues.tsx`; mới `cleaning-personal.tsx`, `housekeeping-workspace.css`, `frontend/tests/housekeeping-workspace-browser.mjs`.

Backend: `Compact/Security/Access.cs`, `Services/AuthService.cs`, `Services/WorkService.cs`, `Services/WorkService.Cleaning.cs`, `Services/WorkService.Housekeeping.cs`, `Services/WorkService.RoomIssues.cs`, `Services/WorkService.RoomIssueConfig.cs`, `Controllers/OperationsControllers.cs`; mới `Services/WorkService.RoomReporting.cs`.

Kiểm chứng: hai script `scripts/verification/housekeeping-{workspace,rework}-runtime.mjs`; tài liệu `docs/ui/compact-ux.md`, plan/report/journal của lượt này. Các file dirty khác có từ trước, không được coi là thay đổi của task này.

## Giới hạn

Không chạy giao dịch vệ sinh thử trên Production; giao dịch đầy đủ được kiểm tra trên TEST. Browser kiểm tra responsive dùng fixture; API thật được kiểm chứng độc lập. Ba test SKIP có sẵn nêu trên chưa được thực thi. Không có blocker đã biết trong phạm vi thay đổi.
