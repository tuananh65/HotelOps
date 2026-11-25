# Bàn giao source và database

Đã chuyển cấp vật tư sang nhận công việc, theo sức chứa tối đa và số vật tư khả dụng còn giữ. Tiêu hao theo thực tế bổ sung. Sửa cộng nhu cầu khăn cho việc tạm dừng và loại phần đã đặt vào phòng; bỏ cấp tự động nhóm B. Cảnh báo thiếu vật tư đầu ca cũ không còn trong inbox/realtime.

Đã nhập chính thức hai chứng từ450/451: mỗi kho8loại nhómA x100đơn vị; backup trước thay đổi, giữ lịch sử cũ. Hạn nội bộ theo quyền Owner; reusable policy theo loại/chi nhánh, chưa theo từng chiếc. Đã đổi tên vai trò Trưởng đội bảo trì/Maintenance Lead và giữ mã quyền cũ.

Các nhóm file chính: StaffSupplyService, WorkService, NotificationService, ReusableAccounting/InventoryService/ShelfLifePolicy, frontend supply-configuration/reusable-inventory, SQL nhập kho/tên vai trò/policy, deployment generator/healthcheck/database-package, tests và hướng dẫn triển khai. Commit913c1595 gom353file thay đổi được Owner yêu cầu đưa lênmain từ toàn phiên; merge b4ca053e bảo toàn lịch sửmain, không thay source.

Kiểm chứng:

- Backend Release build: PASS,0warning0error. Tests166PASS4SKIP; service SQL clone1PASS bổ sung. Bốn integration suites không được cấu hình trong lượt suite chung, không tính làPASS.
- Frontend typecheck/buildPASS;9testsPASS. Hai cảnh báo annotation từ SignalR dependency vẫn còn.
- Deployment/package tests6PASS, bao gồm chống sửa ciphertext và chống ghi đè khi giải mã.
- SQL generator chạy trên database trống:57bảng234FK116CHECK, guards hợp lệ. Chưa chạy Docker/Linux tại local.
- SQL supply clone: startkhôngxuất, acceptđúngnorm, retrykhôngtrùng, thiếu tồn khôngxuất, consumeactual; rollbackfixture. Chưa kiểm thửHTTP/handover.
- Development5173/API5081 và ProductionHTTPS5080: health57PASS; EdgeTLS/login/recovery/API401/404/realtime401PASS.
- Backup cuốiCOPY_ONLYCHECKSUM/VERIFYONLY; giải mã AES256GCM khớpSHA256. Gói công khai6.7MB; khóa/backup thô và hồ sơ nhân sự không đưa vàoGit. Quét credential mẫu mạnh0hit; quétliteralcredential mới0candidate.

Khóa local: deployment/private/hotelops-20261125.key. Hướng dẫn: docs/deployment/main-release-20261125.md. Không triển khai EC2, không gửi email thật. AI/SMTP external vẫn cầncredentials riêng. Pushmain và trạng tháiCI được ghi trong kết quả cuối của phiên.
