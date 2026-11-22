# Form dùng chung gửi mật khẩu tạm qua Gmail

Yêu cầu: Gmail; form đơn giản để Admin gửi mật khẩu tạm đến email tài khoản. Người dùng cho phép không chạy test cho lần điều chỉnh này.

## Thực hiện

- Một form chung `ResetPasswordForm`, dùng trong quản lý tài khoản: email người nhận chỉ đọc, Gmail người gửi, mật khẩu ứng dụng Gmail, mật khẩu HotelOps xác nhận.
- Backend nhận Gmail cho từng request, cố định SMTP Google cổng 587 STARTTLS; không cần cấu hình máy chủ trước. Giữ tương thích request cũ bằng cấu hình SMTP local tùy chọn.
- Không lưu Gmail/app password vào database, browser storage, cấu hình hoặc audit. Xóa ô mật khẩu sau mỗi lần gửi; không trả mật khẩu tạm trong API.
- Giữ quyền USER_MANAGE, kiểm tra phiên bản bản ghi, tài khoản hoạt động, bảo vệ Admin cuối cùng, giới hạn tần suất, mật khẩu tạm 24 giờ, buộc đổi và thu hồi phiên cũ.
- Lỗi SMTP rollback thay đổi mật khẩu. Giới hạn SQL/SMTP không thể commit nguyên tử vẫn áp dụng, đã ghi trong tài liệu yêu cầu.
- Không sửa schema hay dữ liệu nhân sự; không gửi thư thật, không chạy workflow ghi database.

## File tạo/sửa cho điều chỉnh Gmail

- `frontend/src/compact/reset-password-form.tsx` (mới)
- `frontend/src/compact/account-management.tsx`
- `frontend/src/compact/api.ts`
- `src/HotelOps.Api/Compact/Contracts/AccountManagement.cs`
- `src/HotelOps.Api/Compact/Services/AccountManagementService.cs`
- `src/HotelOps.Api/Compact/Services/PasswordResetEmail.cs`
- `tests/HotelOps.Compact.Tests/AccountManagementTests.cs` (đồng bộ chữ ký mail transport)
- `frontend/tests/compact-account-management.mjs` (đồng bộ form mới; chưa chạy lại)
- `docs/requirements/ACCOUNT-PASSWORD-EMAIL.md`
- `plans/261122-1914-account-management/plan.md`
- Báo cáo này và journal cục bộ.

## Kiểm chứng

- Build FE: **Passed** (`npm run build`, TypeScript + Vite), không có cảnh báo.
- Build BE: **Passed** (`dotnet build HotelOps.slnx`), 0 warnings / 0 errors; project test cũng biên dịch thành công.
- Automated tests: **Not Run** cho điều chỉnh Gmail theo yêu cầu người dùng. Kết quả cũ tại `test-261122-1914-account-management.md` không được tính là kiểm chứng luồng Gmail mới.
- Gửi thư Gmail thực tế: **chưa xác minh**, chưa có thông tin người gửi/mật khẩu ứng dụng.
- API đã khởi động lại: PID 43800, cổng 5768, HotelOpsCompactDB, health schema 2.1.0. FE giữ cổng 5186.
- Không commit/push/deploy, không có subagent.

Tham chiếu: [Google về mật khẩu ứng dụng](https://support.google.com/accounts/answer/185833?hl=vi), [Google SMTP](https://developers.google.com/workspace/gmail/imap/imap-smtp?hl=en).
