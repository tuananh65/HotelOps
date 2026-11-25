# Login / khôi phục mật khẩu

## Phạm vi
- Giữ Username-only trong AuthService và AccountInvitationService; không sửa backend, CSS hoặc markup trong lượt tiếp tục này.
- Đổi locator kiểm thử nút khôi phục thành `Gửi liên kết đặt lại` theo giao diện mới.
- Bổ sung kiểm chứng phiên đăng nhập cũ bị thu hồi sau reset, Username không bị EmployeeCode của tài khoản khác che khuất, và EmployeeCode không được dùng để yêu cầu reset.

## Kết quả
- Lượt cuối **19/19 PASS**, 14:55 ngày 25/11: `artifacts/restructure-v2/login-recovery-username-nineteen-pass.json`, log `login-resume-runtime.log`. SMTP sink nhận sáu thư; không có email ra bên ngoài.
- Chuỗi thực tế: tạo/resend bằng UI quản trị; first-login enforcement; SMTP setup; mobile password toggle; quên mật khẩu theo Username; đặt lại và đăng nhập bằng mật khẩu mới; mật khẩu/phiên cũ bị vô hiệu; username không bị employee code che khuất; employee code không yêu cầu reset được; token một lần/hết hạn; lỗi SMTP và retry.
- Cleanup thành công: hai tài khoản tổng hợp giữ inactive, activeAccounts=0; fixture/process metadata đã xóa; không còn listener5778/2526.
- Không sửa C# nên không chạy lại build/test backend trong lượt này; kết quả runtime dùng assembly hiện có. Email provider thực tế và thời gian chờ retry/rate limit đầy đủ chưa được kiểm chứng, chỉ sink và fixture thời gian.
- JavaScript `node --check`: PASS cho bốn runner/helper invitation.
- PowerShell parser: PASS cho fixture.
- Lượt đầu đạt 17 bước, lỗi bước kiểm thử chồng Username/EmployeeCode do fixture `RecoveryReady` đẩy thời điểm tạo token đã hết hạn lên sau ngày hết hạn, vi phạm CHECK `CK_invitation_token`.
- Đã sửa fixture chỉ lùi những bản ghi mới hơn mốc sáu phút; không thay đổi token đã cũ. Không sửa CHECK hay code sản phẩm để làm test xanh.
- Bằng chứng lỗi: `artifacts/restructure-v2/login-recovery-fixture-expiry-failure.json` và `.log`; cleanup sạch, hai tài khoản tổng hợp inactive, không còn tài khoản fixture active.
- Lượt tiếp theo đạt 10 bước rồi nhận 429 khi mở luồng khôi phục: các ca token lỗi trước đó dùng hết chính sách `account-reset` (5 yêu cầu/phút/IP). Đã lưu `login-recovery-rate-window-failure.json/.log`; cleanup sạch. Runner khởi động lại API TEST giữa các nhóm kịch bản để tách quota trong bộ test, giữ dữ liệu/session đã lưu và giữ nguyên rate limit sản phẩm. Không tuyên bố đã kiểm thử độ dài cửa sổ rate limit.
- Lượt cuối chạy toàn bộ chỉ database TEST, SMTP sink localhost2526, API5778; trước khi chạy đã backup CHECKSUM và VERIFYONLY.

## File sửa trong lượt này
- `scripts/verification/v2-invitation-runtime.mjs`
- `scripts/verification/v2-invitation-recovery-browser.mjs`
- `scripts/verification/v2-invitation-fixture.ps1`

Không commit/push, không gửi email thật, không thay mật khẩu tài khoản vận hành.
