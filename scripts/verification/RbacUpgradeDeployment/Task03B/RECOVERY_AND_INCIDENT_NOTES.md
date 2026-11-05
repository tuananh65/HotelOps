# Recovery và ghi nhận sự cố Task03B

Database sử dụng SIMPLE. Backup cuối chỉ bảo vệ trạng thái tại checkpoint; không bảo đảm point-in-time/tail-log recovery cho writes sau backup. Không thay recovery model, ACL, database mode hoặc connection string.

Task03A đã restore độc lập thành công với logical files `HotelOpsDB` và `HotelOpsDB_log`, MOVE sang files riêng. Task03B chỉ kiểm tra VERIFYONLY và file layout của backup mới; không restore vào database khác vì ngoài phạm vi. Không diễn giải VERIFYONLY thành một restore test mới.

Trước COMMIT, candidate có XACT_ABORT và TRY/CATCH rollback transaction của batch cuối. Orchestrator dừng khi lỗi, kiểm tra XACT_STATE/TRANCOUNT và chỉ rollback transaction trên chính connection của nó. Ba batch đầu không cùng transaction với batch cuối; guard yêu cầu data_scope đã có, tránh additive DDL ngoài transaction trên target được duyệt.

Sau COMMIT, nếu verification lỗi: giữ writers dừng nếu có, thu thập bằng chứng chỉ đọc, không ad-hoc fix, không restore đè nguồn. Restore/cutover hoặc compensation cần được đánh giá và phê duyệt riêng. Các database trial/rehearsal được giữ nguyên.

## Lỗi điều phối đã phân tích trước Upgrade

1. Khai báo `param` của helper đặt sai vị trí khiến chế độ DefinitionsOnly không được nhận. Chỉ inspection đã chạy; chưa tạo execution receipt, chưa backup hoặc chạy candidate. Đã chuyển param lên đầu file và kiểm tra thực tế AST ParamBlock cùng import không mở connection.
2. `switch` PowerShell đổi giá trị `$_` thành chuỗi tên cột; nhánh default dùng `$_.name` tạo SQL identifier rỗng. Query SELECT fingerprint user_roles thất bại. Receipt `read-only-harness-error.json` ghi chính xác: 0 batch, chưa backup, transaction 0 và 72 bảng không đổi. Đã thay bằng if/elseif và kiểm tra projection đủ tám cột. Đây là lỗi harness, không phải candidate hoặc dữ liệu; không bỏ guard và không sửa baseline.
3. Một lệnh kiểm tra AST riêng ban đầu dùng biến bị helper ghi đè nên không tìm được expression; đã dùng biến AST riêng và chạy lại kiểm tra thành công. Lệnh kiểm tra này không mở SQL connection.
4. CHECKDB từng chờ `RESOURCE_SEMAPHORE`, blocking_session_id=0; sau đó hoàn tất PASS. Không chỉnh SQL memory, không KILL session, không tăng timeout để ép gate qua.

Các lỗi và giới hạn nêu trên phải được giữ trong báo cáo, không coi lần chạy harness đầu là một lần RBAC COMMIT thất bại.
