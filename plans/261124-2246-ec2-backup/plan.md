# Backup Compact cho EC2
- [x] Đối chiếu SQL Server 2022, schema 3.0.0, schema gate và khả năng Express/Linux.
- [x] Backup COPY_ONLY CHECKSUM, VERIFYONLY và restore sang database mới để kiểm chứng.
- [x] Đóng gói .bak, SHA256, manifest và hướng dẫn restore không ghi đè database hiện có.
- [x] Kiểm tra healthcheck/mapping, ghi rõ commit và giới hạn Linux chưa kiểm chứng trực tiếp.

Người dùng đã cho phép dùng toàn bộ dữ liệu/tài khoản Compact local cho hotelops.ap.vn. Không reset dữ liệu, không dùng legacy, không deploy/push.
