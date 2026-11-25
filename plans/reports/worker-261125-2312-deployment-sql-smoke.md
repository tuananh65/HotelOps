# Kiểm chứng SQL triển khai V2

Đã tạo mới database `HotelOpsCompactDeployTest2611252315`, từ chối nếu tên đã tồn tại. Đã backup database trống bằng COPY_ONLY CHECKSUM và RESTORE VERIFYONLY trước khi cài schema.

Đã chạy SQL sinh bởi `node deployment/render-migration.mjs schema-only HotelOpsCompactDeployTest2611252315` qua SqlClient, tách GO, cùng connection và transaction của generator. Kết quả thực thi: PASS, 57 bảng. Guard cuối generator kiểm tra không có users/items/stock seed và không có FK/CHECK/trigger bị vô hiệu hóa. Role MAINTENANCE_STAFF có trong schema.

`node --check deployment/render-migration.mjs`: PASS. Không sửa generator hoặc database chính thức. Backup metadata ở `artifacts/workday/backup-HotelOpsCompactDeployTest2611252315.json`.

Giới hạn: chưa chạy Docker/Linux hoặc ASP.NET SchemaGate trên database này; đây là bằng chứng thực thi SQL Server local, không phải chứng nhận web triển khai thành công.
