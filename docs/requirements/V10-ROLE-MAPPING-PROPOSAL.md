# Đối chiếu vai trò và quyền V10 — đã duyệt

Ngày: 22/11/2026. Nguồn: ZIP V10 (`app.js`, `v9-enhancements.js`, `v10.js`) và quyền đọc trực tiếp từ HotelOpsCompactDB. V10 là mô phỏng giao diện; bảng dưới là đề xuất triển khai thật, chưa phải quyền đã được duyệt.

## Phương án thay đổi

Không thêm bảng/cột/migration; giữ 40 bảng và database legacy. Backup trước thay đổi dữ liệu. Không xóa vai trò/tài khoản/quyền đang có. Giữ `DEMO_MANAGER` để các tài khoản cũ tiếp tục hoạt động; tạo vai trò riêng để kiểm thử đúng từng actor.

| Actor V10 | Hiện tại | Đề xuất cụ thể | Phạm vi |
|---|---|---|---|
| Admin | SYS_ADMIN có quản trị tài khoản, vai trò, danh mục, tài sản, vật tư | Giữ quyền hiện có; thêm AUDIT_VIEW để xem nhật ký. Dashboard quản trị dùng quyền quản trị sẵn có, không cần DASHBOARD_VIEW vận hành. Cấu hình nghiệp vụ qua danh mục/master data. | ALL |
| Operation Manager | Đang dùng vai trò tổng hợp DEMO_MANAGER | Tạo OPERATION_MANAGER với danh sách bên dưới, tài khoản mẫu demo_om | BRANCH |
| Housekeeping Supervisor | Đang dùng DEMO_MANAGER | Tạo HOUSEKEEPING_SUPERVISOR, tài khoản demo_hks; điều phối, kiểm tra, dọn lại | BRANCH; ca của mình OWN |
| Housekeeping Staff | DEMO_HOUSEKEEPER | Giữ nguyên quyền và tài khoản demo_hk1/demo_hk2/demo_dn_hk | Giữ nguyên |
| Maintenance Lead | Đang dùng DEMO_MANAGER | Tạo MAINTENANCE_LEAD, tài khoản demo_maintenance_lead; phân loại, phân công, kiểm duyệt, lịch định kỳ | BRANCH; ca của mình OWN |
| Maintenance Staff | DEMO_TECHNICIAN | Giữ nguyên quyền và tài khoản demo_tech | Giữ nguyên |
| Storekeeper | STOREKEEPER | Giữ quyền; sửa quản lý vật tư dùng chung để ITEM_MASTER_MANAGE theo BRANCH có thể quản lý vật tư dùng chung đúng phạm vi đã được cấp (hiện API chỉ cho global nên giao diện thiếu thao tác). Thay đổi này cho phép sửa danh mục vật tư chung, ảnh hưởng các chi nhánh dùng vật tư đó. | Cần duyệt riêng lựa chọn bên dưới |
| Department Head | DEPT_MANAGER mới có lịch ca, vật tư, tổng quan, báo cáo | Thêm TASK_VIEW, TASK_MANAGE, IR_VIEW, IR_CREATE, IR_PROCESS, IR_OVERDUE_HANDLE để có các luồng công việc/yêu cầu như V10. Không giới hạn một phòng ban. | BRANCH |
| Owner | Chưa có vai trò | Tạo OWNER, tài khoản demo_owner; chỉ đọc tổng quan/báo cáo và dữ liệu nguồn cần cho báo cáo, không quyền thao tác | ALL |
| Reception | RECEPTION | Giữ nguyên quyền nhận/trả/chuyển phòng; không cấp quyền dọn phòng | Giữ nguyên |
| Auditor ngoài V10 | DEMO_AUDITOR | Giữ nguyên | Giữ nguyên |

## Danh sách quyền chính xác cho vai trò mới

**OPERATION_MANAGER — BRANCH**: DASHBOARD_VIEW, ROOM_VIEW, ROOM_BLOCK_MANAGE, INCIDENT_VIEW, INCIDENT_CREATE, INCIDENT_TRIAGE, INCIDENT_MANAGE, INCIDENT_ESCALATION_HANDLE, MAINTENANCE_VIEW, ASSET_VIEW, ASSET_MANAGE, ASSET_TRANSFER, ASSET_RETIRE, INVENTORY_VIEW, IR_VIEW, IR_CREATE, IR_PROCESS, IR_OVERDUE_HANDLE, REPORT_VIEW, AUDIT_VIEW, TASK_VIEW, CLEANING_VIEW, INSPECTION_VIEW, MR_VIEW. Không cấp nhận/trả/chuyển phòng hoặc quản trị người dùng.

**HOUSEKEEPING_SUPERVISOR — BRANCH**: DASHBOARD_VIEW, ROOM_VIEW, CLEANING_VIEW, CLEANING_ASSIGN, INSPECTION_VIEW, INSPECTION_PERFORM, DEFECT_RESOLVE, INCIDENT_VIEW, INCIDENT_CREATE, CHECKLIST_MANAGE. Thêm ATTENDANCE_RECORD với OWN.

**MAINTENANCE_LEAD — BRANCH**: DASHBOARD_VIEW, ROOM_VIEW, INCIDENT_VIEW, INCIDENT_CREATE, INCIDENT_TRIAGE, INCIDENT_MANAGE, INCIDENT_ESCALATION_HANDLE, MAINTENANCE_VIEW, MAINTENANCE_ASSIGN, MAINTENANCE_REVIEW, PM_MANAGE, ASSET_VIEW, MR_VIEW. Thêm ATTENDANCE_RECORD với OWN. Không tự duyệt công việc do mình thực hiện.

**OWNER — ALL, chỉ đọc**: DASHBOARD_VIEW, REPORT_VIEW, ROOM_VIEW, TASK_VIEW, CLEANING_VIEW, INSPECTION_VIEW, INCIDENT_VIEW, MAINTENANCE_VIEW, IR_VIEW, INVENTORY_VIEW, MR_VIEW, ASSET_VIEW. Các quyền đọc nguồn là điều kiện API báo cáo; giao diện Owner ưu tiên tổng quan/báo cáo, không nút thay đổi dữ liệu.

## Ca làm việc và tài khoản kiểm thử

- Các ca giám sát HK đang gắn DEMO_MANAGER sẽ gắn HOUSEKEEPING_SUPERVISOR; ca trưởng nhóm ENG gắn MAINTENANCE_LEAD. Giữ nguyên giờ, định mức và tổng số ca. Chỉ đổi khi không có phân ca đã lên lịch/đang làm liên quan; nếu có sẽ báo để xử lý, không xóa lịch.
- Bốn tài khoản mới thuộc chi nhánh mẫu Hà Nội; demo_hks có employment HK, demo_maintenance_lead có employment ENG. Owner được phạm vi đọc toàn hệ thống. Mật khẩu kiểm thử lấy cấu hình local hiện tại, không ghi vào source/tài liệu.
- Bổ sung mẫu ca/nhân sự không làm phát sinh bảng. Không đổi giờ ca bạn đã chốt sang giả định 07:30/09:00 trong V10.

## Điểm cần duyệt

1. Duyệt các vai trò mới, quyền bổ sung Admin/Department Head và liên kết vai trò ca đúng như bảng.
2. Vật tư là danh mục toàn hệ thống: chọn giữ quyền sửa vật tư chung cho Admin (Storekeeper quản lý kho/định mức và xem vật tư), hoặc cho Storekeeper sửa danh mục dùng chung. Khuyến nghị giữ Admin quản lý vật tư chung để tránh một thủ kho sửa dữ liệu ảnh hưởng chi nhánh khác.

Người dùng đã duyệt ngày 22/11/2026: “Duyệt bảng; cho thủ kho sửa vật tư chung”. Cho phép thủ kho có ITEM_MASTER_MANAGE tại chi nhánh đang truy cập sửa danh mục items dùng chung; không mở quyền sửa references/roles/branches. Không thay đổi phạm vi các danh mục toàn hệ thống khác.

Đối chiếu tên cơ sở: database thực tế có DEMO-SG (Sài Gòn), DEMO-DN (Đà Nẵng), không có Hà Nội như dữ liệu V10. Bốn tài khoản mới được gắn cơ sở mẫu chính DEMO-SG; không tạo thêm chi nhánh chỉ để khớp tên demo.

Để quyền IR_CREATE/IR_PROCESS của Operation Manager và Department Head hoạt động đúng phạm vi BRANCH đã duyệt, hai vai trò này được chọn bộ phận yêu cầu và điều phối yêu cầu giữa các bộ phận trong chi nhánh của mình. Nhân viên thường vẫn phải thuộc bộ phận yêu cầu hoặc được giao công việc; kiểm tra quyền và chi nhánh trên API được giữ nguyên.
