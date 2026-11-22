> Cập nhật 22/11/2026: bản hiện tại là Compact 2.1.0, 40 bảng, có Workday độc lập. Xem [WORKDAY-INTEGRATION.md](WORKDAY-INTEGRATION.md). Nội dung bên dưới lưu mô tả bản 2.0 để đối chiếu.

# HotelOps Compact v2 — Level 2 Balanced

Ngày kiểm chứng: **21/11/2026**. Database local `HotelOpsCompactDB` đã được dựng lại từ bản v1 rỗng thành **schema 2.0.0, 37 bảng**, theo tài liệu giản lược người dùng cung cấp. `HotelOpsDB` cũ được giữ nguyên, không chuyển dữ liệu. Sau khi chuyển backend, theo yêu cầu tiếp theo đã thêm dữ liệu kiểm thử và tài khoản mẫu vào Compact; xem [TEST-DATA.md](TEST-DATA.md).

## Thay đổi so với v1

- Bỏ ba bảng `shifts`, `workdays`, `time_corrections`. Thời gian thực hiện nằm trong `work_intervals`, gắn trực tiếp với assignment, công việc, người thực hiện và chi nhánh; không cần mở ngày làm việc trước.
- Bổ sung người/thời điểm xác minh trên `work_items`. Cleaning, inspection, rework, incident, maintenance, operational task và internal request dùng chung hạ tầng lưu trữ nhưng giữ loại công việc và ý nghĩa nghiệp vụ riêng.
- Bàn giao backlog dùng `work_events` với người nhận và sự kiện nguồn. Giữ xác nhận nhận/từ chối; chỉ đúng người nhận được quyết định, mỗi đề nghị một quyết định.
- Room readiness dựa trên turnover hiện hành, inspection đạt, rework và room block. Checkout làm phòng DIRTY, tăng turnover và vô hiệu hóa con trỏ bằng chứng cleaning cũ.
- Yêu cầu vật tư, cấp phát và bổ sung hàng dùng chứng từ/dòng/ledger chung. Có cấp từng phần, tính lượng thiếu, tách người duyệt và người cấp; lịch sử đã cấp được bảo vệ.

Không bổ sung bảng ngoài 37 bảng dưới đây. Không có bảng migration; phiên bản nằm trong extended property `HotelOpsCompactSchema`.

## Danh sách bảng

| Nhóm | Số bảng | Tên bảng |
|---|---:|---|
| Cơ cấu và phòng | 5 | `branches`, `departments`, `locations`, `room_types`, `rooms` |
| Người dùng và quyền | 7 | `users`, `memberships`, `roles`, `permissions`, `role_permissions`, `user_roles`, `user_sessions` |
| Danh mục và tài sản | 2 | `reference_values`, `assets` |
| Mẫu công việc và checklist | 3 | `checklist_templates`, `checklist_items`, `work_templates` |
| Thực hiện và xác minh | 7 | `work_items`, `work_links`, `work_assignments`, `work_events`, `work_checklist_items`, `inspection_findings`, `work_intervals` |
| Kiểm soát phòng | 2 | `room_blocks`, `room_events` |
| Kho và chứng từ | 7 | `warehouses`, `inventory_items`, `inventory_policies`, `inventory_documents`, `inventory_document_lines`, `stock_movements`, `document_events` |
| Lịch sử và hạ tầng chung | 4 | `asset_events`, `attachments`, `audit_logs`, `notifications` |
| **Tổng** | **37** | |

Chi tiết cột, khóa và ràng buộc: [01_Schema.sql](../../../scripts/database/compact-v2/01_Schema.sql). Danh sách máy đọc được: [table-manifest.json](../../../scripts/database/compact-v2/table-manifest.json). Đối chiếu toàn bộ **149/149 bảng nguồn**: [source-to-target.csv](source-to-target.csv). Mapping mô tả hướng lưu trữ, không phải script chuyển dữ liệu hoặc chứng nhận tương đương toàn bộ nghiệp vụ.

## Gói SQL và cách chạy

Chạy PowerShell từ thư mục gốc repository, dùng Windows Authentication. Thay giá trị `-Server` theo môi trường; không ghi kết nối local vào appsettings dùng chung.

```powershell
# Chỉ tạo database độc lập chưa tồn tại, tên phải bắt đầu bằng HotelOpsCompact.
./scripts/database/compact-v2/Install-Level2.ps1 -Server '.\SQLEXPRESS' -Database HotelOpsCompactReviewDB

# Kiểm tra catalog và DBCC trên database đã cài.
sqlcmd -S '.\SQLEXPRESS' -E -C -d HotelOpsCompactDB -b -i scripts/database/compact-v2/05_Verify.sql

# Kiểm thử trên database Compact v2 rỗng; mọi fixture đều rollback.
./scripts/database/compact-v2/Test-Compact.ps1 -Server '.\SQLEXPRESS' -Database HotelOpsCompactDB
```

`Install-Level2.ps1` áp dụng lần lượt `01_Schema.sql`, `02_Integrity.sql`, `03_DocumentGuards.sql`, `04_Level2.sql` trong một transaction. File `01_Schema.sql` riêng lẻ chưa đủ các guard và view.

Đường nâng cấp dùng cho lần này là `-RebuildEmptyV1`: chỉ chấp nhận marker **1.0.0**, đúng manifest 40 bảng, các bảng thực sự rỗng và không có procedure/view ngoài dự kiến. Việc kiểm tra hàng dùng khóa độc quyền trong transaction serializable; không dựa vào số hàng ước lượng. `-RollbackTrial` cho phép thử nâng cấp rồi rollback về v1. Cả lần thử và lần áp dụng thật đã thành công. Installer **từ chối chạy đè v2 hoặc database có dữ liệu**; không phải công cụ nâng cấp tùy ý và không cần chạy lại trên `HotelOpsCompactDB` hiện tại.

`Generate-Schema.mjs` tái tạo schema nền/manifest; không thay thế các file guard SQL. `Build-Mapping.mjs` tạo mapping từ inventory nguồn; `Build-Report.mjs` tạo báo cáo từ SRS và hai file `report-introduction.md`, `report-details.md`.

## Kiểm chứng thực tế

| Kiểm tra | Kết quả |
|---|---|
| Database/marker | `HotelOpsCompactDB` / `2.0.0` |
| Catalog | **37 bảng, 152 FK, 77 CHECK, 26 trigger, 6 view** |
| Ràng buộc | FK/CHECK được bật và trusted; không cascade delete |
| SQL regression và Level 2 | **58 đạt, 0 lỗi** |
| Dữ liệu fixture còn lại | **0 hàng** |
| `DBCC CHECKCONSTRAINTS`, `DBCC CHECKDB` | Không phát hiện lỗi |
| Database cũ | Inventory metadata trước/sau có SHA-256 giống nhau; không có thao tác ghi vào `HotelOpsDB` |
| Build C#/API trong lượt thiết kế v2 | Chưa chạy; không sửa code ứng dụng trong lượt này |

Kết quả kiểm thử local nằm ở `artifacts/compact-v2/verification.json` (artifact không commit). Các ca kiểm thử bao gồm đúng scope FK, ledger/reversal/tồn, checklist, thời gian không cần workday, chống overlap và sửa giờ đã chốt, xác minh độc lập, phòng bị block/chưa đạt không nhận khách, turnover mới, xác nhận bàn giao, dependency Internal Request, cấp vật tư từng phần và không vượt phê duyệt. Đây là kiểm chứng schema/SQL, chưa phải kiểm thử API hoặc toàn bộ 144 UC, tải đồng thời hay đánh giá hiệu năng.

Sáu view: `v_stock_balances`, `v_inventory_document_totals`, `v_work_duration`, `v_backlog_handovers`, `v_room_readiness`, `v_material_fulfillment`.

## Backend sử dụng schema v2

Theo yêu cầu tiếp theo của người dùng, backend trong `src/HotelOps.Api/Compact` đã chuyển sang v2 và được nối vào `Program.cs`. API dùng prefix `/api/v2`, context 37 bảng/6 view và startup schema gate; controller/worker legacy không được đăng ký. Xem [BACKEND.md](BACKEND.md) để cấu hình, bootstrap tài khoản, dùng API mới và đọc phạm vi kiểm thử. Các số liệu Build/API trong bảng trên là kết quả của lượt thiết kế database trước khi chuyển backend.

DB bảo vệ các bất biến được mô tả trong SQL; backend bổ sung state machine, quyền theo branch/department, last-admin, dependency trước deactivation, thông báo, audit và giao dịch Reception/housekeeping/kho. View không tự thực hiện các command. Điều chỉnh giờ đã chốt, retention và các ngoại lệ vận hành còn pending vẫn cần hợp đồng rõ ràng, không sửa trực tiếp lịch sử bất biến.

[Báo cáo giản lược hệ thống](../../../HOTELOPS_SYSTEM_SIMPLIFICATION_REPORT.md) đối chiếu đủ **144 UC / FE-01–15**, mô tả luồng, vai trò, quy tắc và các quyết định còn cần phê duyệt. Các nhãn `PROPOSE_REMOVE`/`NEEDS_APPROVAL` vẫn là đề xuất nghiệp vụ; việc dựng schema độc lập theo yêu cầu không đồng nghĩa phê duyệt bỏ mọi xác nhận bàn giao hay xóa lịch sử ở database cũ.
