# Cấu hình nội bộ — hoàn tất

Đã triển khai và cập nhật Production cục bộ https://127.0.0.1:5080/.

## Chức năng

- Trưởng bộ phận có trang Cấu hình nội bộ, hai tab Yêu cầu nội bộ và Báo cáo phòng. Bảng gọn có công tắc xanh/đỏ, không checkbox lớn. Chỉnh sửa từng loại trong form ngắn.
- Loại yêu cầu gồm tên, mô tả, bộ phận nhận mặc định, bật/tắt. Tạo yêu cầu chọn loại đang hoạt động và điền bộ phận nhận mặc định; backend kiểm tra chi nhánh và bộ phận.
- Giữ yêu cầu cũ khi tắt loại. Chi nhánh chưa cấu hình giữ cách tạo trước đó. Khi đã có cấu hình, yêu cầu mới bắt buộc chọn loại hợp lệ.
- Cấu hình có version/audit, yêu cầu vai trò Trưởng bộ phận và IR_PROCESS theo chi nhánh. Báo cáo phòng giữ quyền INCIDENT_TRIAGE hiện có. Không thay schema, entity hoặc grants.
- Chuyển cấu hình loại báo cáo phòng khỏi modal nhiều thẻ sang tab bảng. Thêm mô tả, giữ luồng khắc phục hiện hành. Mobile kiểm tra 320/375/430 px.

## Tệp tạo/sửa trong phần việc này

- Backend mới: `src/HotelOps.Api/Compact/Contracts/InternalRequestTypeRequest.cs`, `Services/WorkService.InternalRequestConfig.cs`.
- Backend sửa trong `src/HotelOps.Api/Compact`: `Contracts/RoomIssueRequests.cs`, `Controllers/OperationsControllers.cs`, `Controllers/WorkspaceController.cs`, `Services/WorkService.cs`, `Services/WorkService.RoomIssueConfig.cs`.
- Frontend mới: `frontend/src/compact/internal-settings.tsx`, `internal-settings.css`.
- Frontend sửa trong `frontend/src/compact`: `app.tsx`, `work.tsx`, `room-issues.tsx`.
- Kiểm thử: `frontend/tests/compact-internal-settings.mjs`, `scripts/database/compact-v2/test-internal-settings.ps1`, `Test-Backend.ps1`.
- Tài liệu: `docs/ui/compact-ux.md`, kế hoạch internal-settings và cập nhật trạng thái kế hoạch Admin.

## Kiểm chứng

- Build .NET: Passed, 0 warnings/errors. Frontend TypeScript/Vite: Passed. Publish Release: Passed.
- `dotnet test HotelOps.slnx --no-build`: 66 passed, 3 skipped. Các test integration có điều kiện của worker/account không được chạy trong lệnh này; không tính là đã qua.
- API/SQL fixture `HotelOpsCompactApiTestfd0e2c92d9d8`: 15 nhóm Passed. Gồm phạm vi/quyền, trùng tên, concurrency, tắt loại, bộ phận mặc định, giới hạn mô tả, lịch sử cũ, audit, loại riêng không lộ trong workspace references và tắt loại khi bộ phận đã ngừng sử dụng.
- Browser internal-settings: Passed cả bản thử và gói release. Browser room-remediation: Passed hồi quy bốn vai trò và mobile. Không có lỗi runtime. Ảnh xem lại: `artifacts/internal-settings-desktop.png`.
- Production: TLS tin cậy, trang5080/bundle mới200, API health200/schema2.1.0/40tables, anonymous API401, không lỗi console. Kiểm thử tạo/sửa nghiệp vụ thực hiện trên fixture; không thêm dữ liệu thử vào Production.
- Readonly database Production xác minh Head có IR_VIEW, IR_PROCESS, INCIDENT_VIEW, INCIDENT_TRIAGE scope BRANCH.
- Backup COPY_ONLY/CHECKSUM/VERIFYONLY trước chuyển bản: `HotelOpsCompactDB_before_workday_20261123_191452879.bak`; receipt `artifacts/workday/backup-HotelOpsCompactDB.json`.
- API PID40444, frontend launcher21612/listener35152. Preview5082/5083 đã dừng. Bundle `index-B6vfyJWG.js`; release `artifacts/production-261123-internal`.

## Lỗi đã xử lý và giới hạn

- Review phát hiện tắt loại bị chặn nếu bộ phận mặc định ngừng hoạt động, và form giữ version cũ sau xung đột: đã sửa, kiểm tra tương ứng.
- Các lần test đầu thất bại do fixture nhân viên có vai trò Head, script dùng cột revoked_by không tồn tại, nhãn form thiếu liên kết rõ, và mock đổi role chưa reload session. Đã sửa nguyên nhân và chạy lại thành công.
- Vite thông báo outDir nằm ngoài frontend và không tự xóa là dự kiến khi tạo thư mục release riêng. Git có cảnh báo chuẩn hóa LF/CRLF; không có lỗi whitespace từ diff check.
- Không commit/push. Không còn lỗi chưa xử lý trong phạm vi yêu cầu này.
