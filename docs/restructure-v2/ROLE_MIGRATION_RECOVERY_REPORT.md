# Phục hồi đội bảo trì

Script: `scripts/database/compact-v2/v2-recover-maintenance.sql`. Chỉ được chạy trên `HotelOpsCompactWorkdayTestV22611250010`, sau backup CHECKSUM/RESTORE VERIFYONLY.

Nguồn phục hồi là audit snapshot `P0_ROLE_MIGRATION/261124-v1`; không suy role từ tên nhân viên. Script phục hồi role/membership và lịch tương lai có provenance, giữ password hash, không đổi các HK Staff không thuộc snapshot. Session liên quan bị thu hồi để quyền mới có hiệu lực.

Kết quả đã ghi: 10 Maintenance Staff phục hồi, 20 HK Staff còn nguyên, không đổi credential; audit ở `artifacts/restructure-v2/maintenance-recovery.json` và `inventory-classification.json`. Năm người mỗi chi nhánh được xác định từ snapshot, không thêm tài khoản mới để làm đầy đội.

Grants Maintenance Staff: MAINTENANCE_VIEW/MAINTENANCE_EXECUTE OWN, ASSET_VIEW BRANCH, ATTENDANCE_RECORD/IR_VIEW/IR_CREATE/IR_PROCESS OWN. Giám sát có MAINTENANCE_ASSIGN BRANCH. Checklist permissions được bổ sung trong script riêng, theo đường role hợp lệ cùng chi nhánh.

Không thay đổi database Production/Develop. Audit migration không thay cho E2E phân công nội bộ, nhận việc, checklist, nghiệm thu; các chuỗi này vẫn cần ghi bằng chứng HTTP/UI đầy đủ.

## Đối chiếu chỉ đọc ngày25/11 18:51

[Audit hiện tại](../../artifacts/restructure-v2/role-recovery-current.json) xác nhận10 người trong audit phục hồi:5 mỗi chi nhánh,10 role bảo trì còn hiệu lực; cả10 Username/password hash và trạng thái tài khoản bằng nguồn local (chỉ lưu số đếm, không xuất hash). Tổng role trong snapshot có cả fixture đang chạy, không dùng làm số nhân viên thực. Nguồn local không được sửa. Bằng chứng này chưa phải login bằng chính người được phục hồi.
