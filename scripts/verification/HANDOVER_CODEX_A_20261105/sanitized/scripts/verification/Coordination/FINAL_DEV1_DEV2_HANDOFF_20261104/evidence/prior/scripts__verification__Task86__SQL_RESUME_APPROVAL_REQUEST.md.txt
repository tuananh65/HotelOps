# Task86 — yêu cầu bổ sung fixture, không tạo/reset database

Trial đã tạo đúng phạm vi, giữ lại: `HotelOps_Canonical_20261104_223000_c01286a1_Fresh` trên `REDACTED_LOCAL_SQL_HOST\SQLEXPRESS`.

Receipt đầu `HTTPS_SQL_RECEIPT.json`: **83 PASS / 1 FAIL**, dừng tại kỳ vọng UC038=200 nhưng SYS_ADMIN thực tế không có CLEANING_VIEW/INSPECTION_VIEW nên trả 403 đúng catalog. Đây là lỗi lựa chọn actor của harness; không yêu cầu đổi source UC038 hoặc grants. Các checks sau điểm dừng chưa chạy.

Exact executable đề xuất: `HttpsResumeProgram.cs`, project `Task86.HttpsResumeVerification.csproj`. Không chạy Prepare-Database, không reset/rerun initial fixture harness, không xóa/ghi đè receipts.

Phạm vi SQL bổ sung cần duyệt:

1. Chỉ trên chín users `t86.*` do harness Task86 vừa tạo: thay password hash thành mật khẩu random mới trong memory để login credential thật. Mật khẩu random của lượt đầu không được lưu/log nên không thể dùng lại; không bypass login hoặc ký synthetic cookie. Không đổi user ngoài Task86.
2. Thêm đúng một user `t86.supervisor`, một UserRole HK_SUPERVISOR/Branch BR001 và một UserBranchAccess BR001, lấy role/branch/department IDs từ SQL. Không thêm/sửa role_permissions, roles, permissions.
3. Tiếp tục các trạng thái revocation/expiry/inactive của users fixture Task86 đã nằm trong approval đầu; bổ sung deterministic SQL lock-wait observation và revoke khi request đang chờ actor lock. Không mutation branch, không bật deactivation.

Guard: exact instance/database/Task17 marker; đúng chín fixture users trước khi bắt đầu, không có user ngoài t86.*; đọc lại authoritative T86_NEW/room/cycle IDs. Nếu fixture shape không đúng: dừng trước ghi. Bảo toàn branches/stock/tasks hiện có, receipts và schema. Giữ trial sau test.

Các GET UC038/037 sẽ dùng HK_SUPERVISOR có grant thật, và xác minh SYS_ADMIN bị 403. Current production source giữ nguyên. Các dependency deactivation vẫn BLOCKED; không cộng UC points.

Yêu cầu riêng này do fixture bổ sung vượt danh sách chín user/chín role assignments trong SQL approval ban đầu; không tự mở rộng quyền.
