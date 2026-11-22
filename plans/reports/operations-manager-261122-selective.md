# Operations Manager — kết quả áp dụng chọn lọc

Giữ các quyết định UX trước: chi nhánh dưới logo, tiêu đề trên navbar, toolbar sát navbar, bộ lọc đóng mặc định, phòng 15 ô/trang và drawer. Không cấp thêm quyền, không sửa schema/entity/staffing hoặc dữ liệu nghiệp vụ chính. Không commit/push/deploy.

Tệp đính kèm tại `43f759ea-1013-47ff-b046-644f7f792ca0` không còn file; đã đọc bản cùng tiêu đề trong `ba49153e-4e8d-420c-b8ad-2a2d5cd6a759`. Áp dụng theo chỉ dẫn của người dùng: chọn lọc và ưu tiên UX hiện có.

## Phạm vi và trạng thái

| Nhóm | Kết quả |
|---|---|
| Điều hướng/RBAC | IMPLEMENTED + VERIFIED: 8 nhóm cho profile chỉ có OPERATION_MANAGER trong chi nhánh; quyền thực quyết định hiển thị. Hồ sơ nhiều vai trò giữ điều hướng chung. SQL chính chỉ đọc xác nhận 24 grant BRANCH, một OM mỗi DVBV/DVCT. |
| Tổng quan | IMPLEMENTED + VERIFIED: thống kê công việc đang xử lý/quá hạn theo 6 loại, tài sản theo tình trạng; tối đa 5 việc quá hạn lâu nhất mỗi loại. Mỗi nguồn kiểm tra quyền riêng. Giữ biểu đồ phòng/công việc/tồn kho hiện có. |
| Phòng | IMPLEMENTED + VERIFIED: occupancy và block độc lập; phòng có khách đang chặn xuất hiện trong bộ lọc chặn. Khóa nút block khi lịch sử chưa tải/lỗi. Giữ 15 ô, drawer, quyền lễ tân tách biệt. API gỡ block không ép READY, không cho gỡ khi source work chưa CLOSED. |
| Buồng phòng | IMPLEMENTED + VERIFIED: dùng Work cho CLEANING/INSPECTION/REWORK, mặc định toàn phạm vi thay vì mine; tìm/lọc server trước phân trang. OM không có nút tạo/phân công/thực hiện/kiểm tra do không có quyền tương ứng. |
| Bảo trì & sự cố | IMPLEMENTED + VERIFIED: nhóm riêng tái sử dụng Work; quyền triage/escalate và quy trình hiện có được giữ. Fixture kiểm chứng tạo, triage, escalate, chặn người chưa được giao thực hiện; chặn OM phân công/duyệt sửa chữa. |
| Công việc & yêu cầu | IMPLEMENTED + VERIFIED: OPERATIONAL/INTERNAL_REQUEST; giám sát việc của nhân viên khác, tìm/lọc phía server, tạo/gửi/chuyển/escalate IR theo luồng hiện có. |
| Tài sản | IMPLEMENTED + VERIFIED: tạo/sửa thông tin qua Catalog dùng chung, thêm tình trạng ban đầu hợp lệ, tìm trên toàn danh sách đã tải, 25 dòng/trang. Vị trí/tình trạng vẫn dùng action endpoint có version/audit. Fixture xác nhận move cùng chi nhánh, từ chối vị trí chi nhánh khác và retire khi còn việc mở. |
| Kho & vật tư | IMPLEMENTED + VERIFIED (UI + RBAC): stock/chứng từ/MR dùng trang hiện có trong cùng nhóm, stock 25 dòng/trang. Không thêm quyền phê duyệt hoặc ghi sổ. Fixture xác nhận OM không tạo receipt. |
| Báo cáo & nhật ký | IMPLEMENTED + VERIFIED: thêm snapshot phòng/tài sản hiện tại; báo cáo cũ tiếp tục dùng quyền miền. Nhật ký chỉ metadata, lọc hành động/loại/mã đối tượng phía server và phân trang 100 dòng; không trả before/after JSON. |

## Kiểm chứng

- Backend: `dotnet build HotelOps.slnx` Passed, 0 warnings, 0 errors.
- Frontend: `npm run build` Passed (TypeScript + Vite). Prettier trên các file FE thay đổi Passed. `git diff --check` không có lỗi whitespace; có cảnh báo LF → CRLF của Git ở package-lock/package/main đã tồn tại trong workspace, không phải cảnh báo build.
- .NET: `dotnet test HotelOps.slnx --no-build` Passed: 28, Failed: 0, Skipped: 2. WorkerIntegrationTests và AccountManagementTests thiếu môi trường fixture riêng, không tuyên bố đã chạy hai test đó.
- API/database: `Test-Backend.ps1 -OperationsManager -Port 5187` Passed 18 nhóm trên `HotelOpsCompactApiTest1e38bf904781`. Có kiểm tra 2 chi nhánh, 403, 409/version, domain grant giảm, source block chưa đóng, quyền chuyên môn, báo cáo và audit. DB chính không nhận ghi thử nghiệm.
- Hai lượt fixture đầu chưa đạt do thiếu PRIORITY và đổi OCCUPIED trực tiếp bị trigger chặn. Đã bổ sung dữ liệu kiểm thử và chạy đủ dọn → inspection → nhận phòng → block; không vô hiệu hóa trigger.
- FE workflow: 4/4 Passed. UI DH: Passed 21 viewports/routes. UX chung: navbar 48, cấu hình, lễ tân/phân trang phòng, lịch, dashboard/tài khoản đều Passed. Test lễ tân cập nhật đúng tên dashboard OM mới, vẫn giữ kiểm tra lễ tân không có dashboard.
- UI OM: Passed 24 route/viewport checks ở 1440/768/375; 8 mục, quyền giảm, mine=false, bộ lọc, phòng có khách/chặn, form tài sản/phân trang, audit tìm kiếm/phân trang. API UI mock toàn bộ, không đăng nhập tài khoản thật hoặc gửi email.
- Bằng chứng: `artifacts/compact-v2/backend/operations-manager-verification.json`, `artifacts/operations-manager/ui-verification.json` và ảnh dashboard/audit. Đã xem trực quan desktop/mobile, sửa nhãn tình trạng và khoảng cách phân trang.
- Lượt kiểm tra mobile sau khi xếp biểu đồ/tables cạnh nhau phát hiện grid item làm dashboard tràn ngang 375px. Đã đặt min-width:0 cho các phần tử trong grid này, giữ cuộn ngang bên trong bảng.

## Không áp dụng nguyên xi / giới hạn

- Không thêm xu hướng hoặc bộ lọc ngày trên snapshot hiện tại khi chưa có dữ liệu lịch sử tương ứng; không tạo số SLA giả. Quá hạn dùng DueAt và các trạng thái terminal hiện hành, không tính thời gian quá hạn từ lịch sử trạng thái.
- Không xây quy trình điều chuyển tài sản liên chi nhánh mới. Move chỉ trong chi nhánh được cấp, đúng endpoint hiện tại.
- Chưa bổ sung mọi tổ hợp bộ lọc phòng/tài sản, sort tùy chọn, danh sách sự cố liên quan ngay trong drawer phòng hoặc form liên kết source work khi chặn. Các khả năng không cần để sửa các lỗi UX/điều hướng đã chọn được giữ nguyên phạm vi hiện tại.
- Không thay business rule incident/asset/IR chuyên sâu chưa được chốt. Các endpoint cũ được tái sử dụng; kiểm thử không chứng minh mọi nhánh lifecycle hoặc hiệu năng tải lớn.
- Assets tìm trên các trang đã tải; báo cáo hiện có giới hạn 10.000 dòng. Audit chưa có tổng toàn bộ, nút Tiếp dựa vào đủ 100 dòng nên có thể tới một trang rỗng ở ranh giới đúng 100.
- Chưa kiểm thử ghi bằng tài khoản thật trên database chính, chưa test gửi email và không liên quan cấu hình SMTP.

## Tệp tạo/sửa trong lượt này

Frontend: `frontend/src/compact/app.tsx`, `work.tsx`, `insights.tsx`, `operations-metrics.tsx` (mới), `rooms.tsx`, `assets.tsx`, `stock.tsx`, `catalog.tsx`, `catalog-specs.ts`, `task-ux.css`; `frontend/package.json`.

Backend: `src/HotelOps.Api/Compact/Services/InsightService.cs`, `RoomService.cs`; `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`.

Kiểm thử: `frontend/tests/compact-operations-manager.mjs` (mới), `compact-reception-rooms.mjs`; `scripts/database/compact-v2/Test-Backend.ps1`, `operations-manager-fixture.sql` (mới), `test-operations-manager.ps1` (mới).

Tài liệu: `docs/ui/compact-ux.md`, `plans/261122-operations-manager-selective/plan.md`, báo cáo này.

Process: API cũ 40104 dừng để build; API mới 12864 cổng 5768, worker tắt, health schema 2.1.0/40 tables đạt. FE 5186 tái sử dụng node 6424/9324. API fixture tự dừng; database fixture được giữ để chẩn đoán.
