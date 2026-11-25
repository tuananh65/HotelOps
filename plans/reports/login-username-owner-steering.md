# Latest Owner steering — 25 November 2026, 14:11

Owner explicitly requests: “Thay mã nhân viên thành tên đăng nhập, không sử dụng mã nhân viên”.

This supersedes the earlier parent task to accept both identifiers. Parent has changed AuthService and AccountInvitationService back to Username-only lookup, and updated auth/forgot-password labels, descriptions and placeholders. Do not restore EmployeeCode aliases or ambiguous-identifier rejection: EmployeeCode is no longer a login identifier.

`login_finish`: adapt runtime tests to authenticate/recover by username, verify a different employee code cannot authenticate or enqueue recovery, and verify another account's EmployeeCode matching a valid Username does not block the username owner. Preserve parent layout changes in login-refresh.css; four viewport browser checks already passed. Parent will review/test integration when your work completes.

14:14: Owner requests compact forgot-password dialog. Parent changed class to `login-recovery-dialog`, main button to **Gửi liên kết đặt lại**, with Hủy and submit on one row. Update browser button locators accordingly; preserve this layout.
