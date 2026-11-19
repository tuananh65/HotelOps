# Dữ liệu và cấu hình Lễ tân

Các script phục vụ môi trường phát triển/demo HotelOpsDB, theo phạm vi đã được người dùng duyệt. Không chạy tự động khi khởi động API và không dùng để triển khai production.

- `ReceptionOwnRequests.sql`: bổ sung IR_VIEW phạm vi OWN cho RECEPTION; không mở quyền xem yêu cầu của người khác.
- `HuyPrimaryMembership.sql`: đối chiếu và cấu hình bộ phận Lễ tân của huy.dinh theo chi nhánh hiện tại.
- `InternalRequestLinks.sql`: ba bảng liên kết yêu cầu nội bộ với công việc, vệ sinh và sự cố đã được duyệt riêng.
- `SeedReadyRooms.sql`: tạo các phòng mô phỏng DEMO-LT-01 đến DEMO-LT-05 với chu kỳ vệ sinh/kiểm tra đạt, checklist và audit. Giữ nguyên phòng demo đã tồn tại, kể cả khi đã nhận/trả khách; không khôi phục chúng về READY.

## Chạy có kiểm soát

Cấu hình `ConnectionStrings:HotelOpsDb` bằng .NET User Secrets của project API. Không lưu chuỗi kết nối vào repository. Tài khoản SQL cần quyền backup và quyền thực hiện đúng gói được chọn.

Từ thư mục gốc, thử gói cần cài:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/development/reception/Install-Reception.ps1 -Package Demo
```

Installer xác nhận tên database, tạo COPY_ONLY backup với CHECKSUM, chạy RESTORE VERIFYONLY, thử transaction và rollback rồi đối chiếu snapshot. Không có `-Apply` thì không lưu thay đổi. Khi đã kiểm tra đúng môi trường và được phép áp dụng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/development/reception/Install-Reception.ps1 -Package Demo -Apply
```

Thay `Demo` bằng `Access` hoặc `Links` khi cần gói tương ứng. Phiếu kết quả nằm trong `artifacts/reception-completion/install-<Package>.json`; backup nằm tại thư mục backup của SQL Server. Không đưa backup, phiên đăng nhập hoặc User Secrets vào Git.

Rollback thử nghiệm xác nhận transaction không lưu thay đổi; đây không phải diễn tập khôi phục toàn bộ database từ backup. Sau khi cài, đăng nhập lại Huy để tải quyền và mở Trạng thái phòng. Dữ liệu DEMO có nhãn rõ ràng, không phải nghiệm thu phòng thật.
