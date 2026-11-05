# Task03B — Main Database RBAC Deployment

Phạm vi được người phụ trách phê duyệt: triển khai candidate Task03A lên đúng `HotelOpsDB` sau maintenance, final preflight và backup; xác minh kết quả. Không thay đổi database khác, Authentication source, EF mappings, cấu hình kết nối hoặc legacy departments.

Candidate: `../Task03A/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql`.

SHA-256: `C1FD3904871107F0284CDC8ADF5CFF8193972B408ECBCBC3F3EBE51AF29B3A64`.

## Bằng chứng

- `preflight-evidence.json`: nguồn dữ liệu thực, identity, processes/services/tasks/SQL sessions, 72 bảng và 5 nhóm schema; không chứa credentials.
- `task03a-package-digests.json`: fingerprint 27 artifact Task03A, gồm bằng chứng lịch sử trong thư mục con.
- `execution-evidence.json`: receipt thực thi, các gate đã qua, batch đã hoàn tất, trạng thái COMMIT và thời gian UTC. Không suy ra thành công từ sự tồn tại của file.
- `read-only-harness-error.json`: lỗi SELECT của script kiểm chứng trước backup/upgrade; 0 batch, 0 backup, transaction 0, 72 bảng vẫn khớp baseline.
- `preservation-baseline.json`: fingerprint trước triển khai và các projection kỳ vọng; không xuất hàng users/password/session.
- `backup-verification.json`: identifier và SQL backup metadata; backup nằm trong thư mục backup của SQL Server ngoài repository. Có thể tra vị trí bằng identifier trong msdb; không lưu đường dẫn đầy đủ ở package này.
- `actual-changeset.json`, `postdeployment-fingerprints.json`, `postdeployment-results.txt`, `release04-results.txt`: chỉ được tạo khi đã thực sự đến bước xác minh tương ứng.
- `DEPLOYMENT_EXECUTION_REPORT.md`: kết luận cuối và giới hạn kiểm chứng.

## Orchestrator

`Inspect-Task03B.ps1` chỉ đọc. Chế độ `DefinitionsOnly` nạp helper, không mở SQL connection. Helper được lấy bằng AST từ Task03A; không chạy lại orchestration backup/restore của Task03A.

`Deploy-Task03B.ps1` kiểm tra target/branch/hash, maintenance, baseline và integrity; tạo một backup mới COPY_ONLY CHECKSUM và VERIFYONLY; chạy bốn batch nguyên văn từ candidate trên cùng connection và dừng ngay khi lỗi. Chỉ batch cuối chứa transaction thay đổi dữ liệu. Receipt có sẵn chặn replay tự động.

Chế độ tiếp tục sau lỗi SELECT chỉ chấp nhận đúng lỗi projection đã phân tích, chưa backup, chưa chạy batch, không có transaction và error snapshot khớp baseline. Receipt lỗi được giữ lại. Không áp dụng chế độ này sau khi Upgrade đã bắt đầu.

Không tự chạy lại orchestrator sau khi deployment hoàn tất. Không chạy Full Schema hoặc Seed bổ sung. Mọi recovery ảnh hưởng database chính phải có kế hoạch riêng.
