# Invitation, thiết lập mật khẩu và email

## Đăng nhập và quên mật khẩu — quyết định Owner mới nhất

Đăng nhập và khôi phục chỉ tra cứu **Username**. EmployeeCode không phải định danh đăng nhập/khôi phục; không đổi giá trị tài khoản đang lưu. Trường hợp mã nhân viên của tài khoản A trùng Username của B vẫn phải xác thực B theo Username.

Popup quên mật khẩu nhận tên đăng nhập, gửi liên kết đặt lại tới email đã đăng ký. Token khôi phục có hiệu lực30phút, dùng một lần; thời hạn24giờ bên dưới thuộc lời mời thiết lập tài khoản. Thông báo công khai không tiết lộ tên đăng nhập tồn tại hay không. Sau reset, mật khẩu và phiên cũ không còn dùng được. Nút gửi là **Gửi liên kết đặt lại**; Hủy đóng popup và không gửi API.

`login-recovery-username-nineteen-pass.json` đạt19/19 HTTP/Edge/SMTP sink, bao gồm các ca invitation trước đây và reset/collision/EmployeeCode. Sáu thư chỉ gửi vào sink localhost; cleanup hai tài khoản TEST inactive và dừng5778/2526. Các lỗi fixture ngày token và quota rate-limit trước đó được giữ riêng, không tính PASS. Runner tách cửa sổ rate-limit giữa nhóm bằng restart API TEST; không kết luận đã kiểm độ dài cửa sổ rate-limit hoặc toàn thời gian backoff. Bố cục bốn viewport kiểm riêng trong `login-layout-browser.json`.

## Lời mời tài khoản

AccountInvitation được tạo trong transaction của tạo tài khoản; delivery worker chỉ xử lý sau commit. Database lưu SHA-256 token và bản token được Data Protection bảo vệ cho outbox, không lưu plaintext token/password. Token dùng một lần, hạn 24 giờ; resend thu hồi lời mời cũ. Password fingerprint bảo vệ lời mời khi mật khẩu đã đổi.

Worker có lease 2 phút, tối đa 5 lần thử và backoff tối đa 60 phút; retry không tạo User mới. Status hiển thị SENT/lỗi/cấu hình chặn, không giả báo gửi thành công. Sau thiết lập mật khẩu, session cũ bị thu hồi; backend chặn nghiệp vụ khi còn cờ bắt buộc đổi mật khẩu. SignalR cũng kiểm tra cờ này.

API: GET/POST `api/v2/invitations/users/{id}`/`resend` theo quyền quản lý; POST `api/v2/invitations/setup-password` anonymous, token + password validation và rate limit. Không log token trong báo cáo kiểm thử.

Khóa cấu hình: Email__Enabled, SmtpHost, SmtpPort, Username, Password, FromAddress, PublicBaseUrl, UseTls, AllowLiveDelivery (cùng prefix Email__). PublicBaseUrl cần HTTPS, chỉ loopback được HTTP. SMTP thật cần AllowLiveDelivery và TLS; mặc định không bật gửi thật. SMTP sink localhost/Mailpit dùng dữ liệu thử và không gửi tới địa chỉ ngoài.

Lưu secret qua .NET User Secrets khi phát triển; môi trường triển khai dùng secret store/environment được bảo vệ, không appsettings/git. Persist Data Protection key ring có bảo vệ khi chạy nhiều instance hoặc restart để outbox giải mã được. Không dán giá trị secret vào chat/log.

Bằng chứng hiện hành: `invitation-admin-ui-fifteen-pass.json` đạt 15 HTTP/local SMTP sink/browser checks, có 4 thư thử được bắt, không email nhân viên thật. Browser quản trị tạo tài khoản và hiển thị lời mời chờ khi SMTP tắt; gửi lại thu hồi lời mời cũ, hiện lời mời mới và không tạo thêm user. Browser login bằng mật khẩu tạm buộc đổi mật khẩu ở hai tab; direct business API bị chặn. Thiết lập qua liên kết SMTP trên mobile thành công, token được bỏ khỏi URL; tab cũ thao tác qua API wrapper nhận 401, xóa session và về màn đăng nhập; mật khẩu mới đăng nhập không còn first-change flag. Hai tài khoản tổng hợp đã inactive sau cleanup; review độc lập không có finding. Lượt đầu tìm row quá sớm trước GET refresh đã được giữ riêng, bổ sung barrier GET có chứa user mới. Delivery thật vẫn BLOCKED_EXTERNAL_CONFIGURATION theo D07.
