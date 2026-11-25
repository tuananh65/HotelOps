# Bản bàn giao main ngày 25/11/2026

Source sử dụng Compact schema 3.0.0 với 57 bảng nghiệp vụ. SQL Server 2022; gói backup chứa HotelOpsCompactDB sau nhập vật tư và đổi tên Trưởng đội bảo trì. Không khôi phục đè database đang tồn tại.

## Database

Repository public chỉ chứa `deployment/database/hotelops-20261125.bak.enc`: gzip rồi AES-256-GCM, có xác thực toàn vẹn. Khóa 256-bit ngẫu nhiên nằm riêng tại `deployment/private/hotelops-20261125.key` trên máy tạo bản phát hành, không có trong Git. Chuyển khóa tới máy chủ qua kênh riêng; đặt quyền 0600. Không gửi khóa vào issue/log/chat công khai.

```bash
node deployment/database-package.mjs decrypt \
  deployment/database/hotelops-20261125.bak.enc \
  /opt/hotelops/shared/hotelops-20261125.bak \
  /opt/hotelops/shared/hotelops-20261125.key
```

SHA256 backup giải mã: `d12bdc41b3634f8cc606099c4acf5a91664c2bfba929fb9b6d58a3d10738ee5b`.
SHA256 gói mã hóa: `db2a910a11c909cac939199b9986523b7343472308d7a19f89d51a92dd445c97`.
Backup được tạo COPY_ONLY, CHECKSUM và RESTORE VERIFYONLY WITH CHECKSUM; thử giải mã khớp SHA256. Chưa xác nhận restore gói này trên Linux trong lượt local.

Sau khi thiết lập Docker, secrets SQL, domain và TLS theo [hướng dẫn AWS](aws-docker.md), dùng:

```bash
bash deployment/init-database.sh --restore /opt/hotelops/shared/hotelops-20261125.bak
```

Script từ chối database đã tồn tại, đọc logical file names và ánh xạ sang đường dẫn Linux. Không chạy lại các script cleanup, recovery nhân viên hoặc nhập kho lịch sử trên database đã khôi phục.

## Thay đổi vận hành

- Bắt đầu ca chỉ tạo sổ vật tư; nhận công việc mới cấp phần thiếu theo sức chứa tối đa. Kiểm đếm và tiêu hao vẫn theo số thực tế bổ sung.
- Chứng từ 450 và 451: mỗi kho 8 loại vật tư nhóm A, mỗi loại 100 đơn vị cơ sở; tổng cộng 1.600 đơn vị. Các chứng từ cũ được giữ.
- Hạn quản lý nội bộ do Owner cho phép lựa chọn: nước uống 25/05/2027; dầu gội, sữa tắm, kem đánh răng 25/11/2027; giấy vệ sinh, bàn chải và vật tư tái sử dụng 25/11/2028. Đây không phải hạn nhà sản xuất được xác minh.
- Vật tư tái sử dụng áp dụng hạn kiểm tra theo loại/chi nhánh, không theo từng chiếc. Quá hạn chặn cấp/đưa vào dùng, vẫn cho hoàn trả và đối soát; không tự ghi giảm sở hữu.
- Tên vai trò là Trưởng đội bảo trì / Maintenance Lead; mã `MAINTENANCE_SUPERVISOR` giữ nguyên để bảo toàn quyền hiện có.

AI và SMTP bên ngoài vẫn cần cấu hình riêng; không chứa credentials trong source hoặc gói hướng dẫn. Push main không đồng nghĩa đã triển khai EC2.
