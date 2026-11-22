# Quản lý tài khoản và mật khẩu tạm qua email

Yêu cầu ngày 22/11/2026 mở rộng UC-005: chỉnh họ tên/email/điện thoại; reset mật khẩu được gửi đến email đã lưu thay cho hiển thị mật khẩu trên giao diện. Không thay đổi role/permission/schema.

Quản lý tài khoản hiển thị thông tin hiện tại và các vai trò/phạm vi còn hiệu lực. Thu hồi dùng các endpoint cũ, giữ nguyên kiểm tra ca làm việc/công việc đang giao và quản trị viên cuối cùng.

Reset yêu cầu USER_MANAGE, xác nhận mật khẩu của người quản trị, lý do và rowVersion hiện tại. Không reset chính mình, tài khoản ngừng hoạt động/ngừng làm việc, thiếu email, hoặc quản trị viên cuối cùng. Mật khẩu được sinh ngẫu nhiên 24 ký tự, chỉ tồn tại rõ trong bộ nhớ và email, có hiệu lực 24 giờ. Thu hồi mọi session, thay security stamp và buộc đổi mật khẩu khi đăng nhập; không ghi mật khẩu vào API/audit/log.

## Form gửi Gmail dùng chung

Theo quyết định tiếp theo ngày 22/11/2026, dùng form Gmail ngay trong **Nhân sự & truy cập → Quản lý → Đặt lại mật khẩu**. Người nhận tự lấy từ email tài khoản, không cho đổi sang địa chỉ khác trong form. Admin nhập Gmail (hoặc Google Workspace), mật khẩu ứng dụng Gmail và mật khẩu HotelOps để xác nhận. Lý do audit được ghi tự động từ thao tác xác nhận.

Không cần cấu hình SMTP trên máy chủ để sử dụng form này. Backend cố định `smtp.gmail.com:587` với STARTTLS và dùng địa chỉ Gmail nhập vào làm người gửi/tên đăng nhập SMTP. Thông tin Gmail chỉ tồn tại trong request/bộ nhớ, không lưu database, cấu hình, browser storage hoặc audit; các ô mật khẩu được xóa sau mỗi lần gửi. Khi triển khai qua mạng phải dùng HTTPS để bảo vệ dữ liệu gửi tới API.

Gmail cần [mật khẩu ứng dụng và xác minh 2 bước](https://support.google.com/accounts/answer/185833?hl=vi); một số tài khoản tổ chức có thể bị chặn chức năng này. Form có liên kết tạo mật khẩu ứng dụng. SMTP sử dụng [máy chủ/cổng do Google công bố](https://developers.google.com/workspace/gmail/imap/imap-smtp?hl=en). Không dùng mật khẩu Gmail thông thường.

Thay đổi form chỉ kiểm tra build theo yêu cầu người dùng, không chạy lại bộ test hoặc gửi thư thật. Các kiểm thử thành công trước đó thuộc luồng SMTP cấu hình trên máy chủ, không chứng minh Gmail đã gửi thành công.

## Cấu hình SMTP local tùy chọn cho API cũ

Kiểm tra ban đầu: chưa có mail service hay khóa SMTP trong User Secrets/environment được kiểm tra. Không tự tạo tài khoản gửi mail hoặc dùng địa chỉ giả.

Lưu các khóa sau bằng **Manage User Secrets** của `src/HotelOps.Api`, hoặc cấu hình môi trường tương ứng; không đặt mật khẩu thật vào appsettings được commit:

| Khóa User Secrets | Giá trị cần cung cấp |
|---|---|
| `PasswordResetEmail:Host` | Máy chủ SMTP của nhà cung cấp |
| `PasswordResetEmail:Port` | Cổng STARTTLS, mặc định 587 |
| `PasswordResetEmail:From` | Địa chỉ người gửi đã được nhà cung cấp cho phép |
| `PasswordResetEmail:Username` | Tên đăng nhập SMTP |
| `PasswordResetEmail:Password` | Mật khẩu ứng dụng hoặc mật khẩu SMTP |

Ví dụ tên biến môi trường: `PasswordResetEmail__Host`. Không dán mật khẩu vào chat, log hay dòng lệnh có lưu lịch sử. Khởi động lại API sau khi cập nhật cấu hình môi trường. TLS luôn được yêu cầu và chứng thư được kiểm tra mặc định. Transport dùng STARTTLS; không dùng cổng SMTPS 465. Theo [tài liệu Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.enablessl?view=net-10.0), SmtpClient không hỗ trợ TLS bắt đầu ngay trên cổng 465.

Khi request không cung cấp Gmail và máy chủ cũng thiếu cấu hình, API trả 503 EMAIL_NOT_CONFIGURED và giữ nguyên mật khẩu. Gmail nhập thiếu hoặc sai định dạng bị từ chối trước khi đổi mật khẩu. Lỗi SMTP/timeout cũng rollback thay đổi. API thành công nghĩa là máy chủ SMTP đã chấp nhận thư, không bảo đảm thư đã vào inbox (nhà cung cấp vẫn có thể bounce/lọc spam).

## Giao dịch và giới hạn

SQL và SMTP không có giao dịch phân tán chung. Backend kiểm tra và SaveChanges trong transaction, gửi mail với timeout 15 giây, rồi commit. Lỗi gửi mail không thay đổi mật khẩu/phiên/audit. Trường hợp hiếm SQL commit thất bại sau khi SMTP nhận thư có thể tạo thư chứa mật khẩu không sử dụng được; thao tác vẫn báo lỗi và quản trị viên cần thực hiện lại. Không tự động thử lại để tránh gửi lặp.

Kiểm thử reset có ghi dữ liệu chỉ chạy trên database sao chép `HotelOpsCompactWorkdayTestAccount...`, với transport giả nhận mật khẩu trong bộ nhớ; không gửi email hoặc reset mật khẩu nhân viên thật. Kiểm thử giao diện chặn các request ghi. Đối chiếu gửi đến inbox thật cần cấu hình SMTP hợp lệ và một người nhận kiểm thử được chỉ định.
