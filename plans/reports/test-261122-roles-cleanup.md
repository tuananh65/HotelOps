# Kết quả chỉnh trang Vai trò — 22/11/2026

## Thay đổi đã thực hiện

- Bỏ cột Mã trên bảng vai trò; mã nội bộ và quy tắc phân quyền giữ nguyên.
- Xóa vật lý DEMO_MANAGER và DEMO_AUDITOR đã ngừng hoạt động, cùng 47 liên kết quyền và 3 lần gán tài khoản đã thu hồi. Không có ca làm tham chiếu hai vai trò này.
- Chuẩn hóa tên Quản trị hệ thống và mô tả tiếng Việt cho 10 vai trò còn lại; bỏ nội dung mô tả seed/demo.
- Nút Chỉnh sửa nền xanh nhạt, Phân quyền nền xanh đậm; cùng rộng 112px, cao tối thiểu 44px, cách nhau 8px. Desktop nằm ngang; màn hình nhỏ xếp dọc. Có viền focus khi dùng bàn phím.

## An toàn dữ liệu và bằng chứng

Backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công trước dry-run và áp dụng:
`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261122_182105405.bak`.

Giao dịch SERIALIZABLE kiểm tra đúng tập vai trò/người dùng/quyền phụ thuộc, từ chối nếu còn lần gán chưa thu hồi hoặc ca làm tham chiếu. Dry-run rollback thành công trước khi áp dụng. So sánh toàn bộ các dòng gán vai trò/quyền còn lại trước và sau bằng EXCEPT theo hai chiều.

- 10 vai trò hoạt động, 56 tài khoản và 56 lần gán vai trò hiện hành.
- 131 liên kết quyền của các vai trò đang hoạt động giữ nguyên; chỉ 47 liên kết của vai trò đã ngừng bị xóa.
- 40 bảng; không có khóa ngoại disabled/untrusted.
- Audit 253 lưu JSON hợp lệ với 12 vai trò trước và 10 vai trò sau, kèm các dòng liên kết bị xóa. Không thay đổi nhật ký cũ.
- Dữ liệu bằng chứng: `artifacts/role-cleanup/{backup,dry-run,applied,verification,ui-results}.json`.

## Kiểm chứng

- Frontend build: **Passed**, không có cảnh báo build.
- `npm run test:compact`: **Passed**, 4/4.
- `tests/compact-roles-ui.mjs`: **Passed** trên FE 5186 / API 5768 / HotelOpsCompactDB thực tế.
- API trả đúng 10 vai trò hoạt động, không còn hai vai trò mẫu, mọi mô tả có nội dung tiếng Việt.
- Bảng chỉ còn Tên, Mô tả, Trạng thái, Thao tác; 10 LED hoạt động.
- Tìm kiếm “bảo trì” trả 2 vai trò. Hai nút mở đúng hộp thoại; quyền Admin vẫn chỉ đọc, 67 quyền hiển thị.
- 1440/768/375px: kích thước nút bằng nhau, có khoảng cách, không tràn chiều ngang toàn trang, không lỗi JavaScript. Đã xem ảnh chụp desktop và mobile.
- Ảnh: `artifacts/role-cleanup/roles-{1440,768,375}.png`.
- `git diff --check`: Passed; Git có cảnh báo chuyển LF/CRLF ở các file đã thay đổi từ trước.

## File thuộc thay đổi này

- Sửa: `frontend/src/compact/catalog.tsx`, `frontend/src/compact/task-ux.css`.
- Tạo: `frontend/tests/compact-roles-ui.mjs`.
- Tạo: `scripts/database/compact-v2/Inspect-RoleCleanup.ps1`, `Invoke-RoleCleanup.ps1`, `role-cleanup-20261122.sql`.
- Tạo/cập nhật: kế hoạch `plans/261122-1817-role-cleanup/plan.md`, báo cáo này, nhật ký AgentKit của lượt chỉnh vai trò.

## Giới hạn

Không sửa C# hoặc API contract; .NET build/test không chạy lại. Kiểm thử UI mở hộp thoại, không lưu sửa vai trò hoặc cấp quyền thử vào database chính. Bảng trên điện thoại cuộn ngang nội bộ để giữ đủ nội dung. Backup đã VERIFYONLY, không thực hiện phục hồi thử. Script xóa có kiểm tra điều kiện và sẽ từ chối chạy lại sau khi đã áp dụng.

Giữ frontend PID 41940/launcher 35444 và backend PID 42448 phục vụ người dùng; trình duyệt kiểm thử đã đóng. Không commit/push/deploy; không chạy agent phụ.
