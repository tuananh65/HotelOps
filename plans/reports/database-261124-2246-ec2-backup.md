# Bàn giao backup EC2 — 24/11/2026 22:46

Đã tạo file phù hợp cho nhánh feature/project2-role-alignment-20261123, đối chiếu commit ea212388904d389c906d92136470baada8c19501.

- File: artifacts/ec2-backup-261124/package/HotelOpsCompactDB_schema3_20261124_ec2.bak (29.058.560 byte).
- SHA256: 9831086ce5de9c31f55728209ff41ab01c2a350543539bf2d95851f85458c93a.
- Nguồn SQL Server 2022 Express 16.0.1000.6; Compact 3.0.0, 49 bảng, 205 FK, 97 CHECK, 38 trigger, 6 view; không constraint bị tắt hoặc không trusted.
- Người dùng cho phép sử dụng dữ liệu/tài khoản local trên hotelops.ap.vn. Giữ 57 tài khoản (55 hoạt động, có SYS_ADMIN), 96 phòng hoạt động và 199 chứng từ, gồm 194 chứng từ TEST.
- COPY_ONLY/CHECKSUM, RESTORE VERIFYONLY WITH CHECKSUM, restore thực tế từ chính file bàn giao, DBCC CHECKDB, EF mappings và API health: Passed.
- Build / unit tests: Not Run trong lượt này vì không đổi code ứng dụng. Dùng Release đã kiểm thử ở lượt trước.
- Tệp tạo: plan; báo cáo này; docs/deployment/ec2-compact-backup-20261124.md; package gồm .bak, SHA256SUMS, manifest.json, restore-linux.sql, database-audit.json và api-health.json; helper kiểm chứng nằm trong artifacts/ec2-backup-261124.
- Không reset/sửa dữ liệu nguồn, không dùng legacy, không deploy EC2, không commit/push. Giữ hai database kiểm chứng riêng; API tạm đã dừng.
- Export trực tiếp từ thư mục SQL bị Access denied; xuất qua OPENROWSET cũng bị quyền Windows. Đã tạo backup mới vào thư mục bàn giao riêng được cấp quyền cho đúng SQL service, sau đó verify/restore lại. Không đổi ACL thư mục SQL hệ thống.

Giới hạn: chưa thực hiện restore trên Linux/EC2; khả năng tương thích dựa trên nguồn SQL Express 2022, metadata không có tính năng edition-specific và hướng dẫn Microsoft. Workspace có code chưa commit; backup không chuyển các thay đổi code đó lên nhánh remote. Hướng dẫn chi tiết và liên kết nguồn tại docs/deployment/ec2-compact-backup-20261124.md.

Không còn câu hỏi chờ người dùng.
