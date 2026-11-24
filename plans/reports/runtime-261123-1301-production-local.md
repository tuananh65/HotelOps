# Chạy bản Production trên máy hiện tại

- Frontend: http://127.0.0.1:5186 — bản build Vite production được phục vụ bằng Vite preview cho chạy tại máy; không phải triển khai hosting công khai.
- Backend: https://localhost:5769 — ASP.NET Core môi trường Production, publish Release tại `artifacts/production-261123-1301/api`.
- Database: HotelOpsCompactDB hiện có. Không chạy migration/reset/seed. Worker nền tiếp tục tắt như cấu hình trước đó.
- HTTPS API dùng chứng chỉ localhost hiện có; proxy frontend xác minh chứng chỉ qua bản public PEM, không xuất private key.
- PID backend: 36528; frontend: 1484 (launcher 12472). Đã dừng API Debug 34228 và Vite development 6424 của đúng Project 2. Các ứng dụng khác được giữ nguyên.
- Kiểm tra: health HTTPS 200, schema 2.1.0, 40 bảng; frontend 200, trả asset đã build và không có Vite dev client; endpoint auth qua proxy trả 401 đúng khi chưa đăng nhập.
- Publish thành công. Có một cảnh báo MSB3026 do tệp font license tạm bị khóa; retry thành công.
- Log và ảnh kiểm tra nằm tại `artifacts/production-261123-1301/`.

Không commit/push. Đây là bản chạy Production cục bộ, chưa cấu hình tự khởi động cùng Windows hoặc public domain.
