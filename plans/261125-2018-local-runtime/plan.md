# Hai môi trường local

Nguồn:Owner25/11 yêu cầu chạy Development và Production,cả hai local.

- [x] Kiểm tra baseline/cấu hình/cổng.
- [x] Tạo clone riêng từ backup CHECKSUM/VERIFYONLY, không ghi đè.
- [x] Release static frontend và HTTPS; API unknown404.
- [x] Build0warnings,155PASS/3SKIP; frontendPASS với PURE warnings.
- [x] Chạy dev5173/API5080,ProductionHTTPS5443; worker bật trên DBriêng.
- [x] Edge/nativeTLS/APIboundary/login/recovery smokePASS.
- [x] Tài liệu start/stop/status và giới hạn.

Hai runtime giữ chạy theo yêu cầu; metadata artifacts/local-runtime. Không có approval đang chờ. Không đổi branch,commit,push hoặc deployEC2. AI/SMTP ngoài vẫn tắt; key local có cảnh báo chưa mã hóa, đã ghi rõ.
