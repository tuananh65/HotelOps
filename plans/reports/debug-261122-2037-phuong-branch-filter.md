# PhuongDH1 không hiện khi lọc Bùi Viện và Quản lý vận hành

## Nguyên nhân và sửa lỗi

SQL Server HotelOpsCompactDB xác nhận lúc 13:39 UTC ngày 22/11/2026: PhuongDH1 là Đặng Hoài Phương, user 435, mã OM002, tài khoản và nhân sự ACTIVE. Membership 39 là BRANCH_ACCESS tại Bùi Viện (DVBV, 413), còn hiệu lực, không có EMPLOYMENT/bộ phận. Vai trò assignment 12 là OPERATION_MANAGER (9), tên hiển thị **Quản lý vận hành**, tại chi nhánh 413, còn hiệu lực.

API danh sách chỉ trả `employments`; frontend chỉ dùng mảng này để lọc chi nhánh. Vì vậy người có quyền truy cập chi nhánh nhưng chưa có bản ghi công tác bị loại nhầm. Lần chạy regression trên API cũ tái hiện lỗi thiếu phạm vi chi nhánh.

Đã bổ sung `branches` vào DTO danh sách từ các membership EMPLOYMENT/BRANCH_ACCESS còn hiệu lực, khử trùng chi nhánh. Bộ lọc chi nhánh sử dụng danh sách đó, vẫn kiểm tra vai trò trong cùng chi nhánh hoặc vai trò toàn hệ thống. Bộ lọc bộ phận tiếp tục dùng công tác thực tế; không tự suy ra hoặc gán bộ phận. Đổi nhãn “Chi nhánh công tác” thành “Chi nhánh” cho đúng phạm vi. Không sửa schema, membership, role hoặc quyền.

## File tạo/sửa

- `src/HotelOps.Api/Compact/Contracts/UserDirectory.cs`: DTO phạm vi chi nhánh.
- `src/HotelOps.Api/Compact/Services/UserService.cs`: đọc phạm vi chi nhánh trong danh sách tài khoản.
- `frontend/src/compact/account-filters.tsx`: lọc chi nhánh và nhãn.
- `frontend/tests/compact-account-filters.mjs`: cập nhật kỳ vọng chi nhánh và fixture; chưa chạy lại toàn bộ script cũ.
- `frontend/tests/compact-branch-access-filter.mjs`: mới, regression API/browser thật cho PhuongDH1.
- Báo cáo này, journal `plans/journals/2026-11-22-hotelops-phuong-branch-filter.md`, bằng chứng trong `artifacts/branch-access-filter/`.

## Kiểm chứng

- Backup trước đăng nhập kiểm thử (đăng nhập có tạo session): `HotelOpsCompactDB_before_workday_20261122_204042368.bak`, đã xác minh.
- FE build: **Passed**.
- BE build: **Passed**, 0 warning/0 error ở lần cuối. Lần đầu thất bại với MSB3027/MSB3021 và 10 cảnh báo retry vì API PID 2964 khóa DLL; đã xác minh đúng tiến trình HotelOps, dừng riêng tiến trình đó rồi build lại thành công.
- `dotnet test HotelOps.slnx --no-build`: **28 Passed, 2 Skipped, 0 Failed**. Hai test worker/account integration thiếu fixture cấu hình nên bị bỏ qua.
- API GET `/api/v2/users` và browser thật: **Passed**. Bùi Viện + vai trò Quản lý vận hành trả về đúng một tài khoản PhuongDH1; tìm username hiển thị Đặng Hoài Phương; chọn bộ phận hoặc chi nhánh khác không hiện sai; xóa lọc phục hồi danh sách.
- PhuongDH1 vẫn có 0 employment; không thêm membership/role. Không có lỗi JavaScript. Đã xem ảnh `phuong-filtered.png`; bằng chứng `verification.json`.
- API đã khởi động lại, PID **31332**, cổng **5768**, database HotelOpsCompactDB, worker tắt; FE tiếp tục ở 5186. Các browser test đã đóng.

Kết nối SQL trong sandbox ban đầu lỗi SSPI; truy vấn chỉ đọc ngoài sandbox thành công. `git diff --check` không có lỗi whitespace; cảnh báo LF/CRLF thuộc ba file package.json/package-lock.json/main.tsx đã đổi từ trước, không sửa trong lượt này. Không phát hiện lỗi còn lại trong phạm vi kiểm tra; không commit/push/deploy.
