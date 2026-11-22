# Cập nhật dữ liệu V2 và ERD

Hoàn tất 22/11/2026, một agent. Database: HotelOpsCompactDB. Không sửa source C#/FE, schema, EF entities, migrations hoặc database legacy. Không commit/push/deploy.

## Dữ liệu đã áp dụng

Nguồn: HotelOps_Codex_Dieu_Phoi_Trien_Khai_Project2_V2.docx và Excel HotelOps_Ma_Tran_Du_Lieu_Phan_Quyen_Cho_Phe_Duyet_V1.xlsx trong Downloads. Đã đọc toàn bộ sheet. Yêu cầu trực tiếp “sửa lại dữ liệu trên db” cùng hai câu trả lời sau đó xác nhận ngừng role mẫu, thu hồi hai quyền Admin và dùng địa chỉ/điện thoại Excel. Không thực hiện các workflow/code changes khác chỉ vì được nhắc trong tài liệu.

| Branch ID giữ nguyên | Mã cũ | Mã mới | Tên mới | Địa chỉ | Điện thoại |
|---|---|---|---|---|---|
| 413 | DEMO-SG | DVBV | Chi nhánh Bùi Viện | Bùi Viện | 0987654321 |
| 414 | DEMO-DN | DVCT | Chi nhánh Cao Thắng | Cao Thắng | 0987654312 |

| Role ID giữ nguyên | Mã cuối cùng | Tên cuối cùng | Trạng thái |
|---|---|---|---|
| 1 | SYS_ADMIN | System Administrator | Hoạt động |
| 3 | RECEPTION | Lễ tân | Hoạt động |
| 4 | HK_STAFF | Nhân viên buồng phòng | Hoạt động; mã trước là DEMO_HOUSEKEEPER |
| 5 | MAINTENANCE_STAFF | Nhân viên bảo trì | Hoạt động; mã trước là DEMO_TECHNICIAN |
| 6 | STOREKEEPER | Thủ kho | Hoạt động |
| 7 | DEPT_MANAGER | Trưởng bộ phận | Hoạt động |
| 9 | OPERATION_MANAGER | Quản lý vận hành | Hoạt động |
| 10 | HOUSEKEEPING_SUPERVISOR | Giám sát buồng phòng | Hoạt động |
| 11 | MAINTENANCE_LEAD | Trưởng nhóm bảo trì | Hoạt động |
| 12 | OWNER | Chủ sở hữu | Hoạt động |
| 2 | DEMO_MANAGER | Giám sát vận hành mẫu | Ngừng |
| 8 | DEMO_AUDITOR | Người xem báo cáo mẫu | Ngừng |

ID 1–10 trong Excel là thứ tự mục tiêu, không được dùng ghi đè PK. Đã đổi mã/tên trên cùng bản ghi nên user_roles, role_permissions và workday_shifts giữ liên kết bằng ID.

Admin mất ASSET_MANAGE/ALL và ITEM_MASTER_MANAGE/ALL, còn 5 quyền quản trị. Không thêm permission mới. Storekeeper giữ quyền sửa vật tư chung và các quyền STOCK đã có. Department Head giữ SHIFT_MANAGE/BRANCH; API hiện cho đọc lịch cả 4 bộ phận trong chi nhánh và từ chối chi nhánh khác.

Hai role mẫu chỉ đổi is_active=false, giữ lịch sử và 47 grants. Không cộng quyền sang vai trò khác, không chuyển tài khoản. demo_manager, demo_dn_manager, demo_auditor vẫn là tài khoản hiện có nhưng không còn role/grant hiệu lực. Các thao tác nghiệp vụ trả403. Không thay mật khẩu.

Không thay 8 department: tài liệu ghi toàn bộ manager=NULL, nhưng live DB có manager_user_id=431 tại department211 (HK) và214 (STORE). Giữ nguyên dữ liệu live vì chưa cung cấp người quản lý thực tế thay thế. Đổi branch bằng ID tự cập nhật tên chi nhánh qua JOIN; không cần đổi khóa ngoại department.

## Backup và giao dịch

- Database thử: HotelOpsCompactWorkdayTestV2Data20261122, restore từ backup chính rồi thử cập nhật trước.
- Backup ngay trước cập nhật chính, đã CHECKSUM/VERIFYONLY: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261122_150916177.bak`.
- [Metadata backup](../../artifacts/workday/backup-HotelOpsCompactDB.json).
- Script kiểm tra database, baseline, mã nguồn/đích, tài khoản Admin, công việc/ca đang mở của role mẫu; áp dụng trong transaction với XACT_ABORT và application lock.
- Audit: entity_type=DataNormalization, entity_key=V2-DATA-20261122, action=APPLY, lưu before/after và hai grants bị thu hồi. actor_user_id=NULL vì đây là cập nhật vận hành theo yêu cầu người dùng, không giả danh tài khoản ứng dụng.
- Các câu lệnh kiểm tra bên trong transaction xác nhận user_roles, departments, workday_shifts và PK không đổi; không có grant mới/sửa ngoài hai grant được thu hồi.
- Nếu cần rollback: ưu tiên khôi phục backup vào một database riêng rồi đối chiếu before_json của audit để phục hồi đúng các dòng đã thay đổi. Không restore đè database chính nếu đã có dữ liệu phát sinh mới. Chưa thực hiện rollback vì cập nhật đạt kiểm tra.

## Kết quả kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| Database thử | PASS: 40 bảng/160FK, 10 role hoạt động, 178 grants, giữ ID/liên kết |
| Database chính | PASS: giống kết quả trên; DBCC CHECKCONSTRAINTS không có vi phạm |
| Danh mục permission | 67 mã, giữ nguyên |
| Role vật lý / hoạt động | 12 / 10; 47 grants thuộc role ngừng, 131 thuộc role hoạt động |
| Branch / department | 2 / 8; không thay timezone, trạng thái hoặc manager |
| API | PASS 8 tài khoản; Admin hai POST bị403, role mẫu không còn quyền, HK/maintenance hoạt động, thủ kho giữ quyền, Department Head giới hạn chi nhánh |
| FE | PASS Admin/HK; tên, mã và số điện thoại chi nhánh mới hiển thị |
| ERD | PASS 40 bảng, 493 cột, đủ 160 FK; schema trước/sau dữ liệu giống nhau |
| ERD viewer | PASS 8 chế độ, 40 lựa chọn bảng, phóng to/thu nhỏ, chọn bảng, tải SVG, desktop/mobile, 0 lỗi JavaScript |
| Build C#/FE | NOT RUN: không thay source ứng dụng |
| Bộ unit/regression toàn hệ thống | NOT RUN: chỉ chạy kiểm tra trực tiếp liên quan thay đổi dữ liệu; không lấy kết quả lượt cũ thay kết quả lượt này |

Bằng chứng: [API](../../artifacts/erd/api-verification.json), [FE](../../artifacts/erd/frontend-verification.json), [DB trước](../../artifacts/erd/live-data-before.json), [DB sau](../../artifacts/erd/HotelOpsCompactDB-after.json), [render](../../artifacts/erd/render-validation.json), [viewer](../../artifacts/erd/viewer-verification.json).

Test API đầu tiên đọc sai DTO catalog (b.code thay vì b.fields.code), đã sửa harness và chạy lại cả 8 tài khoản đạt. Không thay ứng dụng để làm test qua.

## ERD và tệp bàn giao

- [ERD xem offline, phóng to và tra cứu](../../docs/database/compact-v2/erd/HotelOps-ERD.html).
- [SVG tổng thể](../../docs/database/compact-v2/erd/overview.svg), [Mermaid đầy đủ](../../docs/database/compact-v2/erd/hotelops-full.mmd), [hướng dẫn và đối chiếu160FK](../../docs/database/compact-v2/erd/README.md).
- Mỗi nhóm chức năng có tệp .svg/.mmd. Tổng thể chỉ hiện PK để giảm rối; sơ đồ nhóm và sidebar thể hiện các cột còn lại. Full Mermaid có493cột.
- Hai FK tự tham chiếu reference_values được gộp nhãn F081/F082 trên cùng đường vì renderer chỉ giữ một self-loop cùng cardinality. Cả160FK vẫn được đối chiếu;159đường vật lý. Quan hệ đa hình không có FK thật không được tự vẽ thêm.
- Script mới: [08_V2DataNormalization.sql](../../scripts/database/compact-v2/08_V2DataNormalization.sql), [Apply-V2DataNormalization.ps1](../../scripts/database/compact-v2/Apply-V2DataNormalization.ps1).
- Công cụ xuất/kiểm tra/renderer và snapshot nằm trong artifacts/erd, artifacts/erd-tools. Không thay dependencies ứng dụng. Không khởi động server hay agent phụ; browser kiểm tra đã đóng.

File Word danh mục đã bàn giao trước đó là snapshot trước cập nhật; không ghi đè để tránh làm mất mốc đối chiếu. Tải lại frontend để nhận tên/quyền mới. Địa chỉ chi tiết hơn, người quản lý thực tế, dữ liệu phòng và các chính sách chưa chốt khác không được tự bổ sung trong lượt này.
