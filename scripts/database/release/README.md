# HotelOps — Database Release (Authentication + RBAC)

**Ngày:** 21/10/2026
**Hệ quản trị:** Microsoft SQL Server 2022 (compatibility level 160)
**Phạm vi:** Bàn giao cho Backend DEV1 / DEV2.
**Trạng thái:** Đã kiểm thử cài mới và một trường hợp nâng cấp trên các database **thử nghiệm độc lập**. Template Upgrade dùng chung **chưa được kiểm thử cho mọi dữ liệu của thành viên khác**.

## 1. Chọn phương án sử dụng

| Tình trạng hiện tại | Phương án | Dữ liệu cũ |
| --- | --- | --- |
| Chưa có `HotelOpsDB` | **A — Full Installation** | Tạo DB trống, không có dữ liệu cũ |
| Có DB đã hoàn thành FE-01 Authentication | **B — Authentication → RBAC Upgrade** | Thiết kế để giữ dữ liệu nghiệp vụ; bắt buộc backup và kiểm thử bản sao |
| DB cũ chưa có FE-01 Authentication | Nâng cấp Authentication bằng script đã duyệt **trên bản sao**, sau đó mới làm B | Cần kiểm thử riêng cả hai bước |
| Đã có RBAC mới | **Không tự ý chạy lại Upgrade** | Xác minh trạng thái trước và lập migration tiếp theo nếu cần |

**Không dùng Full Installation để nâng cấp DB đang tồn tại. Không chạy script trên `HotelOpsDB` gốc/DB chung khi chưa có xác nhận của nhóm.**

## 2. Cấu trúc file trong Git

```text
scripts/database/
├── authentication/
│   ├── 001_authentication_schema_update.sql
│   └── 001_authentication_schema_rollback.sql
└── release/
    ├── 01_HotelOps_Full_Schema.sql
    ├── 02_HotelOps_Upgrade_Auth_To_RBAC.sql
    ├── 03_HotelOps_RBAC_Catalog_Seed.sql
    ├── 04_HotelOps_Verification_ReadOnly.sql
    └── README.md
```

- **01 — Full Schema:** Tạo DB mới tên `HotelOpsDB` với bảng, khóa, index, CHECK, DEFAULT và FK. Từ chối nếu tên DB đã tồn tại. **Không** tạo tài khoản, chi nhánh, mật khẩu hay dữ liệu demo.
- **02 — Upgrade:** **Template cần người thực hiện cấu hình và rà soát**, không phải script nâng cấp một lần nhấp cho mọi DB. Chuẩn hóa mã role, catalog permission, scope và kiểm tra phân nhiệm. Mục tiêu DB được đặt dưới dạng placeholder.
- **03 — RBAC Seed:** Nạp catalog chính thức bằng `role_code` / `permission_code`, không dựa vào ID số. Dùng **sau 01**, **không chạy thêm sau 02**.
- **04 — Verification:** Script **chỉ đọc**, kiểm tra tổng role/permission, scope, bộ phận cũ, OM/DH và Owner. Cần chọn đúng Database trên SSMS.

## 3. Phương án A — Full Installation (DB mới)

1. Xác nhận trên SQL Server đích **chưa tồn tại `HotelOpsDB`**. Nếu tên đó đã có, dùng SQL Server instance riêng hoặc chuẩn bị bản cài mới cho database thử nghiệm khác; **không ghi đè**.
2. Mở `01_HotelOps_Full_Schema.sql` bằng SSMS, chạy **một lần**, kiểm tra tab Messages không có lỗi. Nếu script dừng vì DB đã tồn tại, **đóng query tab này** trước khi làm việc khác: preflight sử dụng `SET NOEXEC ON`.
3. Trong **tab query mới**, mở và chạy `03_HotelOps_RBAC_Catalog_Seed.sql`. Script tự kiểm tra DB đích.
4. Chọn `HotelOpsDB` trên thanh công cụ SSMS, mở `04_HotelOps_Verification_ReadOnly.sql` và chạy.
5. Chỉ số mong đợi: **12 role** (10 active, 2 inactive), **62 permission**, **140 role–permission grant**. `OWNER` chỉ có `DASHBOARD_VIEW:ALL` và `REPORT_VIEW:ALL`; không có scope `DEPARTMENT` trong phân quyền cuối cùng.
6. DB mới **chưa có branches, users, departments, phân công nhân sự hay thông tin đăng nhập**. Khi cần demo/test API, chuẩn bị Seed Data nhân sự riêng bằng quy trình được duyệt.

## 4. Phương án B — Nâng cấp từ Authentication

**Cảnh báo:** Script Upgrade thực sự cập nhật role và permission, gồm việc xóa **grant cũ trong bảng `role_permissions`**. Nó không nhằm xóa giao dịch nghiệp vụ, nhưng vẫn là thao tác thay đổi quyền ảnh hưởng đến user đang có. Kết quả thử nghiệm trên một DB mẫu không chứng minh an toàn cho mọi DB.

1. **Backup** DB và kiểm tra khả năng Restore. Phục hồi ra **một database thử nghiệm độc lập**, không chạy trực tiếp trên DB gốc.
2. Kiểm tra FE-01 Authentication đã hoàn thành: cần có `users.failed_login_count`, `users.locked_until`, `users.temporary_password_expires_at`, `user_sessions.session_key_hash`. Cột `role_permissions.data_scope` đã có hoặc sẽ được bổ sung bởi pre-step.
3. Tạo **bản sao làm việc** của `02_HotelOps_Upgrade_Auth_To_RBAC.sql`. Thay `__REVIEWED_TARGET_DATABASE__` bằng tên DB thử nghiệm đã xác nhận tại **3 vị trí thực thi**: lệnh `USE` và **hai điều kiện `DB_NAME()`**. Giữ nguyên guards, không tắt hoặc xóa chúng. Tìm lại để bảo đảm không còn placeholder tại vị trí thực thi.
4. Rà soát OM/DH từng chi nhánh: mỗi branch chỉ có tối đa một `OPERATIONS_MANAGER` và một `DEPT_MANAGER` đang hoạt động; scope phải thuộc chi nhánh tương ứng. OM không được giữ quyền `MANAGER` ở branch ngoài phạm vi.
5. Nếu có nhiều DH cùng chi nhánh, điền lựa chọn thực tế đã kiểm tra vào bảng `@SelectedDh`. **Không sao chép mặc định các User ID mẫu** (như `(1,4),(2,19)`) sang DB của người khác. DH kiêm Storekeeper, gán HK Inspector/Supervisor trùng và quyền OM đa chi nhánh phải được giải quyết bằng migration riêng có người phê duyệt. Script dùng chung **cố tình THROW để dừng**, không tự đoán.
6. Các bộ phận không thuộc mô hình cốt lõi `HK/MT/WH/FO` (ví dụ `LAU`, `SEC`, `FB`) và gán nhân sự mẫu **không tự động bị xóa/chuyển** bởi script dùng chung. Script cũng không tự tạo OM, Storekeeper, Owner user hay mật khẩu.
7. Chạy **bản đã điền DB đích, đã review** trên database thử nghiệm. Đây là script **COMMIT**, không phải Dry Run. Các thay đổi catalog/role nằm trong transaction và rollback nếu một điều kiện kiểm tra thất bại.
8. **Lưu ý rollback:** Nếu `data_scope` chưa tồn tại, bước thêm cột diễn ra **trước transaction chính**. Khi phần sau lỗi, cột mới có thể **vẫn tồn tại** dù các thay đổi RBAC khác rollback. Cần Restore từ backup hoặc xử lý riêng, không khẳng định toàn bộ schema đã rollback.
9. Sau khi chạy thành công, chọn đúng DB đích và thực thi `04_HotelOps_Verification_ReadOnly.sql`. Kiểm tra role, scope, các tài khoản quản lý và những cảnh báo nghiệp vụ của database đó.
10. Chỉ khi **chính database của từng thành viên** đã vượt qua kiểm tra trước/sau nâng cấp, nhóm mới xem xét kế hoạch áp dụng lên DB dùng chung. Mọi thao tác lên DB thật phải được review riêng.

**Quy định với template trên Git:** File `02` được commit **giữ nguyên placeholder** `__REVIEWED_TARGET_DATABASE__`. Không commit bản cá nhân đã thay placeholder bằng `HotelOpsDB` hoặc tên DB của DEV1/DEV2. Đừng bao giờ chạy template khi chưa cấu hình và xác nhận database.

## 5. Hợp đồng RBAC đã chốt

- **10 role Active:** `SYS_ADMIN`, `OPERATIONS_MANAGER`, `DEPT_MANAGER`, `HK_SUPERVISOR`, `HK_STAFF`, `MAINTENANCE_LEAD`, `MAINTENANCE_STAFF`, `STOREKEEPER`, `RECEPTION`, `OWNER`.
- **2 role Inactive:** `HK_INSPECTOR` và `STAFF`. Có thể còn grant lịch sử nhưng Backend **phải chặn** role Inactive.
- **Một OM và một DH cho mỗi chi nhánh**. OM quản lý **một** cơ sở, không phải Tổng giám đốc toàn hệ thống. `DEPT_MANAGER` là mã role DB của **Department Head**, quản lý phạm vi bốn bộ phận `HK`, `MT`, `WH`, `FO` tại một cơ sở; OM và DH ngang cấp, quyền khác nhau theo nghiệp vụ.
- HK Supervisor và Maintenance Lead có thể có nhiều người theo ca; Storekeeper có thể một hoặc nhiều người theo quy mô kho. Không được tự duyệt yêu cầu do mình lập, tự nghiệm thu phần mình thực hiện hoặc vượt phân nhiệm chỉ vì giữ nhiều vai trò.
- `OWNER`: **chỉ xem** `DASHBOARD_VIEW:ALL`, `REPORT_VIEW:ALL`; không có quyền tác nghiệp.
- Role–Permission là mức quyền nền tảng. Backend còn phải kiểm tra user/role active, hạn hiệu lực, quyền chi nhánh, phạm vi `OWN`, phân công ca/công việc, trạng thái nghiệp vụ, nguyên tắc SoD và audit.

## 6. Kết quả kiểm thử đã ghi nhận (21/10/2026)

| Phép thử | Kết quả |
| --- | --- |
| Tạo schema mới trên `HotelOps_Full_Test` | PASS |
| Seed catalog vào `HotelOps_Full_Test` | PASS: 12 role / 62 permission / 140 grant |
| Verification sau cài mới | PASS: 10 active role, Owner 2 quyền ALL |
| Bản Upgrade đã review cho `HotelOps_Upgrade_Test` | Dry Run và COMMIT PASS |
| Verification RBAC sau Upgrade | PASS: 12 / 10 active / 62 / 140 |
| So sánh bảng, cột, kiểu dữ liệu giữa hai DB | 0 khác biệt được báo cáo |
| Đếm FK, CHECK, DEFAULT, index trên hai DB | Cùng 201 / 40 / 78 / 337 |
| So sánh định nghĩa FK, CHECK, DEFAULT và index nằm trong phạm vi script | 0 khác biệt được báo cáo |

**Giới hạn kết quả:** Bản Upgrade đã chạy trên `HotelOps_Upgrade_Test` có các lựa chọn nhân sự mẫu cụ thể (User 4/19 làm DH, User 1 làm OM chỉ ở Branch 1). Các ID đó **không phải cấu hình phổ quát**. Kiểm thử đã xác nhận hai đường cài đặt trên dữ liệu thử nghiệm, **chưa xác nhận template chung trên DB thực tế của DEV2**, chưa kiểm thử Authorization Core/API và chưa chứng nhận sẵn sàng production.

## 7. Checklist trước bàn giao / Pull Request

- [ ] Người nhận xác nhận tên DB đích, backup và khả năng restore.
- [ ] File Upgrade trên Git còn nguyên placeholder, không có tên DB máy cá nhân, User ID hoặc mật khẩu hard-code.
- [ ] Đã giải quyết xung đột OM/DH/Storekeeper và các department cũ trên **DB cụ thể** cần nâng cấp.
- [ ] Đã kiểm thử trên DB của người nhận và chạy Post-Upgrade Verification.
- [ ] Đã triển khai, kiểm thử Backend Authorization Core (branch/OWN/SoD/audit).
- [ ] DEV1/DEV2 review PR trước khi merge. Không tự chạy trên production hay merge nhánh khi chưa được thống nhất.

**Nguồn chuẩn:** File Full Schema cho cấu trúc; RBAC Catalog Seed cho quyền chính thức; dữ liệu demo/Authentication session/user thực không nằm trong bản cài schema + quyền.
