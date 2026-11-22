# Bộ lọc chi tiết tài khoản

Đã chuyển nút **+ Tạo tài khoản** cạnh **Bộ lọc**, bỏ dropdown trạng thái và thêm panel thu gọn với **Chi nhánh công tác / Bộ phận / Vai trò**. Các tiêu chí kết hợp với tìm kiếm tên, mã nhân viên, username, email; có số kết quả, số tiêu chí đang áp dụng và Xóa lọc. Đổi chi nhánh sẽ xóa lựa chọn bộ phận cũ. Đóng panel giữ nguyên tiêu chí.

Chi nhánh và bộ phận lấy từ EMPLOYMENT còn hiệu lực. Vai trò phải đang hoạt động, đã được gán, chưa thu hồi/hết hạn. Bộ lọc kết hợp không lấy vai trò ở chi nhánh khác để khớp phân công; vai trò toàn hệ thống vẫn được tính. Tài khoản chưa có phân công vẫn có trong danh sách khi không giới hạn công tác.

## File triển khai và kiểm thử

- Tạo `frontend/src/compact/account-filters.tsx`: panel, tiêu chí và logic lọc theo phạm vi.
- Sửa `frontend/src/compact/admin.tsx`: tích hợp toolbar, giữ điều kiện USER_MANAGE cho nút tạo.
- Sửa `frontend/src/compact/task-ux.css`: bố cục toolbar và panel responsive.
- Tạo `src/HotelOps.Api/Compact/Contracts/UserDirectory.cs`: DTO phân công/vai trò đọc.
- Sửa `src/HotelOps.Api/Compact/Services/UserService.cs`: thêm employments/roles vào resource hiện hữu, vẫn kiểm tra USER_VIEW, không trả entity hay thông tin bí mật.
- Tạo `frontend/tests/compact-account-filters.mjs`: đối chiếu SQL, lọc đơn/kết hợp, dữ liệu nhiều chi nhánh, truy cập 401/403, thao tác bàn phím, dialog tạo và responsive.
- Sửa `frontend/tests/compact-task-ux.mjs`: cập nhật kiểm thử bỏ dropdown trạng thái.
- Kế hoạch: `plans/261122-1855-account-filters/plan.md`; nhật ký AgentKit lưu local.

## Kết quả

- `npm run build`: **Passed**.
- `dotnet build HotelOps.slnx`: **Passed**, 0 cảnh báo, 0 lỗi.
- `dotnet test HotelOps.slnx --no-build`: **26 Passed, 1 Skipped**. Bài worker cần database fixture riêng, không liên quan bộ lọc.
- `node tests/compact-account-filters.mjs`: **8/8 Passed**.
- `node tests/compact-task-ux.mjs`: **12/12 Passed**, bao gồm sticky header, tìm kiếm và các actor khác.
- API / SQL: phản hồi 56 tài khoản khớp snapshot SQL độc lập, 53 phân công công tác và 56 gán vai trò còn hiệu lực. Truy cập không đăng nhập 401, HK staff 403. Không lộ passwordHash/securityStamp.
- Kiểm tra ảnh 375/768/1440 px; không tràn ngang trang. Ảnh chụp tắt animation để không bắt trúng thời điểm sidebar đang chuyển bố cục.
- Runtime: FE 5186 trả HTTP 200; BE 5768 PID 5304 trả health ok, schema 2.1.0, 40 bảng. Đã dừng API cũ PID 42448 để build, không đụng các API khác.

## Bằng chứng và giới hạn

- `artifacts/account-filters/results.json`, `sql-directory.json`, `accounts-*.png`; hồi quy `artifacts/admin-ux/results.json`.
- Backup COPY_ONLY/CHECKSUM đã VERIFYONLY: `HotelOpsCompactDB_before_workday_20261122_190058192.bak`; metadata `artifacts/workday/backup-HotelOpsCompactDB.json`.
- Không thay đổi schema, quyền hoặc dữ liệu nghiệp vụ. Đăng nhập kiểm thử ghi session sau backup. Dialog tạo chỉ được mở/đóng, không tạo tài khoản thử trên DB thật.
- Giữ giới hạn 500 tài khoản vốn có của endpoint; đã đối chiếu toàn bộ 56 tài khoản hiện tại. Chưa kiểm thử tải trên 500 tài khoản.
- Các lần đầu harness thất bại do SQL FOR JSON bỏ field rỗng và selector getByLabel exact chứa nội dung option. Đã xác minh accessible name, đổi sang combobox theo tên, kiểm tra mảng API bắt buộc và giữ nguyên đối chiếu từng bản ghi. Không sửa sản phẩm để che lỗi kiểm thử.
- Hook chặn đọc `.env.local`; đã bỏ thao tác không cần thiết này, không truy cập lại hay thay đổi cấu hình.
- Không có lỗi sản phẩm còn mở trong phạm vi thay đổi. Không commit/push/deploy.
