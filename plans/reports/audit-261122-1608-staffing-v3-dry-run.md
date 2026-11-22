# HotelOps — đối chiếu nhân sự V3, chỉ đọc

Đã đối chiếu đủ **15 tài khoản hiện hữu và 39 hồ sơ mới**, tổng **54** theo Excel V3. Không phát hiện trùng mã nhân viên, username hoặc email; toàn bộ role mục tiêu tồn tại và hoạt động. **Chưa sẵn sàng áp dụng toàn bộ** vì còn phạm vi Owner sai khác, 3 chuyển role chờ duyệt, bảo vệ bootstrap và phương án cấp tài khoản/lịch ca chưa chốt. **Không thực hiện bất kỳ ghi database, đăng nhập, cấp quyền, kích hoạt hoặc gửi email nào.**

## Kết quả bắt buộc

| Chỉ tiêu | Kết quả |
|---|---|
| DATABASE_TARGET | HotelOpsCompactDB — 40 bảng |
| EXISTING_USERS_VERIFIED | 15/15; User ID 424–438 đều tồn tại, không có user khác hoặc soft-deleted ngoài danh sách |
| NEW_USERS_PLANNED | 39; toàn bộ User ID mới để trống |
| TOTAL_TARGET_USERS | 54 |
| DVBV_ADDITIONS | 16 |
| DVCT_ADDITIONS | 23 |
| EMP_CODE_COLLISIONS | 0 |
| USERNAME_COLLISIONS | 0 |
| EMAIL_COLLISIONS | 0 |
| ROLE_MAPPING_ISSUES | 0 mã mục tiêu thiếu/ngừng; 3 chuyển role chưa được duyệt; 1 xung đột scope Owner |
| BRANCH_DEPARTMENT_MAPPING | 39/39 mới hợp lệ; 15 hiện hữu khớp employment/branch membership, riêng scope role Owner cần chốt |
| SHIFT_COVERAGE_GAPS | Có; bảng phủ ca bên dưới. DVCT chưa có người được cấp SHIFT_MANAGE |
| DRY_RUN_STATUS | COMPLETED_WITH_BLOCKERS / NOT_READY_TO_APPLY |
| WRITE_AUTHORIZATION | NOT_GRANTED |
| BLOCKERS | B01–B09, sheet Can_duyet; chi tiết bên dưới |
| NEXT_APPROVAL_REQUEST | Chốt bootstrap, Owner scope, 3 chuyển role, chế độ tạo tài khoản và quyền/activation; sau đó duyệt phạm vi ghi riêng |

## Nguồn và bằng chứng

- Word yêu cầu: `C:/Users/Hax/Downloads/HotelOps_Codex_Staffing_Handoff_V3.docx`; SHA256 `07DFFEFC571CAA25878B5B27A8BA38CA8A619EE3C3573C4697E5EC2197F999E3`.
- Excel mục tiêu: `C:/Users/Hax/Downloads/HotelOps_Nhan_Su_Gia_Lap_Chuan_Hoa_De_Duyet_V3_Codex.xlsx`; SHA256 `918B8224D931AA60F8D14ACEE0ACB256FEA23793158208F282E7953B6BE55BDC`.
- Đọc đủ 10 sheet, kiểm tra 15 dòng `Nhan_su_de_xuat`, 39 dòng `Nhan_su_bo_sung_39`, 54 dòng `Roster_du_kien_54`; các trường định danh/role/branch/department và tổng định biên khớp giữa các sheet. Không có công thức hoặc giá trị lỗi Excel. Không sửa tài liệu đầu vào.
- Snapshot nhân sự và đối chiếu SQL: [users-snapshot.json](../../artifacts/staffing-v3/users-snapshot.json), [diagnostics.json](../../artifacts/staffing-v3/diagnostics.json), [collisions.json](../../artifacts/staffing-v3/collisions.json). Mỗi file có timestamp UTC; lần xác nhận collision đầu hoàn chỉnh lúc `2026-11-22T09:15:14Z` (16:15 giờ Việt Nam).
- 15 users, 15 user_roles, 32 memberships, 12 roles/10 active, 67 permissions, 178 role_permissions, 30 định nghĩa ca, **0 phân công Workday**. Truy vấn chỉ `SELECT`/CTE; không thử INSERT rồi rollback.
- Database và cả 3 cột định danh dùng `SQL_Latin1_General_CP1_CI_AS` (không phân biệt hoa/thường, có phân biệt dấu). So sánh bằng SQL `OPENJSON` có tham số, cùng collation thực; bao gồm toàn bộ users, kể cả tài khoản ngừng/soft-deleted, loại chính User ID khi so tên mục tiêu với bản ghi của nó. Có kiểm tra độc lập case-insensitive trong 54 mục tiêu.
- Unique indexes: `UX_users_employee_code`, `UX_users_username`, `UX_users_email` (lọc `email IS NOT NULL`, không bỏ qua soft-deleted). Không có trùng trong dữ liệu hiện tại, trong mục tiêu, hoặc mục tiêu với account khác trong DB. Không cần đổi username qua giá trị tạm để tránh hoán đổi.
- Mã EMP001–EMP049 và ADM001/OM001/OM002/DH001/OWN001 hợp lệ; username đúng mẫu tên không dấu + chữ viết tắt họ/tên đệm + số; hậu tố `2` giải quyết trùng phần gốc. Email đúng username + `@ducvuong.com`. Không xác minh mailbox hoặc gửi thư.
- BOOTSTRAP hiện thuộc 424. Không tìm thấy cấu hình username bootstrap trong User Secrets đã đọc; chưa xác minh override môi trường của tiến trình API. Điều này không thay thế kiểm thử đăng nhập/khôi phục.

ExcelJS không đọc được cấu trúc workbook nguồn (lỗi `workbook.sheets undefined`); đã đọc trực tiếp các phần XML trong ZIP, giữ tọa độ ô và kiểm tra chéo roster. Không sửa thư viện hoặc suy luận file bị hỏng. Báo cáo Excel mới đã đọc lại thành công bằng ExcelJS, đối chiếu 3.911 ô, 11 sheet, không công thức. Chưa kiểm tra hiển thị trong Excel desktop.

## Thay đổi dự kiến — chưa được phép thực hiện

File rà soát: [HotelOps_Staffing_V3_DryRun.xlsx](../../docs/database/compact-v2/staffing-v3-dry-run/HotelOps_Staffing_V3_DryRun.xlsx). Manifest đầy đủ before/after, rowversion, liên kết nguồn và điều kiện: [staffing-v3-manifest.json](../../docs/database/compact-v2/staffing-v3-dry-run/staffing-v3-manifest.json). Manifest là dữ liệu mô tả, không phải script chạy migration.

| Nhóm | Số dòng dự kiến | Cách xử lý đề xuất sau duyệt |
|---|---:|---|
| users hiện hữu | 15 UPDATE, 60 giá trị | Đổi employee_code, username, full_name, email; giữ nguyên User ID, phone, manager, trạng thái, mật khẩu và lịch sử. 424 cần gate riêng |
| users mới | 39 INSERT | EMP011–EMP049; giữ ID trống cho tới khi SQL sinh. Đề xuất INACTIVE, password NULL, không email; employment_status và quy trình credential còn chờ chốt |
| user_roles của 3 người đổi role | 3 thu hồi + 3 INSERT | Giữ assignment ID cũ/lịch sử, đánh revoked_at sau duyệt, tạo assignment mới đúng scope; không ghi đè role_id bản ghi lịch sử |
| user_roles của 39 người mới | 39 INSERT | Chỉ sau duyệt cấp quyền riêng; chưa cấp quyền trong bước tạo hồ sơ bị khóa nếu chỉ duyệt staging |
| memberships mới | 78 INSERT | Mỗi người: BRANCH_ACCESS (primary=true, department=NULL) + EMPLOYMENT (primary=false, đúng bộ phận) |
| memberships hiện hữu | 32 giữ nguyên | Không tự đóng DEPARTMENT_AUTHORITY của 425/433 hoặc đổi manager |
| roles, permissions, role_permissions, branches, departments, workday_shifts | 0 | Dùng master hiện hữu, không thêm/đổi quyền gốc hoặc cấu trúc |
| audit mới | Chốt theo triển khai | Khi ghi được duyệt phải có before/after, actor, batch; lượt dry-run không tạo audit DB |

Nếu sau này duyệt toàn bộ **trừ thay đổi scope Owner**, kỳ vọng users= 54, memberships=110, user_roles=57 bản ghi gồm 3 đã thu hồi; 54 assignments còn hiệu lực về role/date nếu tất cả điều kiện thỏa. Quyền runtime của người mới vẫn bằng 0 khi account INACTIVE; không nhầm số assignment với account đã kích hoạt. Số bảng vẫn 40, role_permissions vẫn 178. Thay scope Owner sẽ là một phương án riêng chưa được cộng vào số lượng trên.

### Ba thay đổi role

| User ID | Role hiện tại (đã ngừng) | Role đề xuất / ID thực | Branch | Quyền hiệu lực hiện tại → nếu duyệt |
|---|---|---|---|---|
| 425 | DEMO_MANAGER / 2 | HOUSEKEEPING_SUPERVISOR / 10 | DVBV / 413 | 0 → 11 |
| 432 | DEMO_AUDITOR / 8 | RECEPTION / 3 | DVBV / 413 | 0 → 9 |
| 433 | DEMO_MANAGER / 2 | OPERATION_MANAGER / 9 | DVCT / 414 | 0 → 24 |

Số quyền là permission paths đang active, chưa phải kết quả gọi API; `OWN` còn cần sở hữu đối tượng, `BRANCH` cần membership và dữ liệu cùng chi nhánh, phiên login hợp lệ. Danh sách permission/scope cụ thể có trong manifest và sheet Role_grants_DB. Không chuyển 34 grants của DEMO_MANAGER hay13 grants của DEMO_AUDITOR sang role mới. Các quyền nghiệp vụ như kiểm tra/nghiệm thu của HKS và quản lý tài sản/sự cố của OM phải được người duyệt xác nhận phù hợp nhiệm vụ, không chỉ dựa vào tên role.

### Ánh xạ chi nhánh/bộ phận

| Chi nhánh | Branch ID | HK | ENG | FO | STORE |
|---|---:|---:|---:|---:|---:|
| DVBV | 413 | 211 | 212 | 213 | 214 |
| DVCT | 414 | 207 | 208 | 209 | 210 |

Tất cả đang active. Role IDs mục tiêu: SYS_ADMIN1, RECEPTION3, HK_STAFF4, MAINTENANCE_STAFF5, STOREKEEPER6, DEPT_MANAGER7, OPERATION_MANAGER9, HOUSEKEEPING_SUPERVISOR10, MAINTENANCE_LEAD11, OWNER12. Không dùng số thứ tự Excel làm khóa DB. Role và chi nhánh được lặp lại giữa nhiều nhân viên là hợp lệ, không phải lỗi trùng account.

**Xung đột Owner cần chốt:** `Nhan_su_de_xuat!O16`/`Quyen_can_duyet!E16` nói role tại DVBV. Thực tế user_role_id 15 của 438 có branch_id=NULL và 12 grants ALL; chỉ membership 42 có branch413. Giữ nguyên thực trạng cho tới khi có quyết định: giữ global hay thu hẹp về DVBV. Không tự coi đây là thao tác bổ sung ALL hoặc âm thầm thu hẹp quyền.

**Bootstrap:** 424 là Admin duy nhất; đề xuất BOOTSTRAP→ADM001, demo_admin→NamNH1 phải được duyệt riêng cùng kế hoạch kiểm thử và khôi phục. Giữ 5 grants hệ thống hiện tại; không thêm quyền vận hành đã thu hồi.

## Định biên và phủ ca

| Role | DVBV: hiện hữu theo mục tiêu + mới = tổng | DVCT: hiện hữu theo mục tiêu + mới = tổng |
|---|---|---|
| HK_STAFF | 2 + 8 = 10 | 1 + 9 = 10 |
| MAINTENANCE_STAFF | 1 + 4 = 5 | 0 + 5 = 5 |
| HOUSEKEEPING_SUPERVISOR | 2 + 2 = 4 | 0 + 4 = 4 |
| RECEPTION | 2 + 1 = 3 | 0 + 3 = 3 |
| MAINTENANCE_LEAD | 1 + 1 = 2 | 0 + 2 = 2 |
| Bổ sung | **16** | **23** |

48 hồ sơ thuộc5 role vận hành +6 hồ sơ khác = 54. Cột “đã có V2” là **role mục tiêu**, không phải quyền đã được áp dụng. Nếu chưa duyệt đổi425/432, DVBV chỉ có3 HKS và2 Reception sau khi thêm 39, chưa đạt4/3. Tất cả số phủ ca dưới đây giả định đã duyệt/cấp quyền/kích hoạt mục tiêu; hiện không tạo lịch thật.

| Nhóm / chi nhánh | Ca × người mỗi ca | Người mục tiêu | Thiếu suất/ngày nếu tối đa 1 ca/người |
|---|---:|---:|---:|
| HK Staff / mỗi chi nhánh | 3 × 4 =12 | 10 | 2 |
| Maintenance Staff / mỗi chi nhánh | 2 × 3 =6 | 5 | 1 |
| HKS / mỗi chi nhánh | 3 × 1 =3 | 4 | 0 trước khi tính ngày nghỉ |
| Reception / mỗi chi nhánh | 3 × 1 =3 | 3 | 0, không dự phòng |
| Maintenance Lead / mỗi chi nhánh | 2 × 1 =2 | 2 | 0, không dự phòng |
| Kho DVBV | 2 × 1 =2 | 1 | 1 |
| Kho DVCT | 2 × 1 =2 | 0 | 2 |

Ca thực trong DB: HK/HKS/Reception 00–08,08–16,16–24; ENG/Lead/Store 08–11:30 và13–17. Hai ca ENG/Store là hai khoảng sáng/chiều, không phải hai ca 8 giờ. Phép tính trên theo ràng buộc không mặc định ca kép của Word; nếu cho phép một người bao trọn sáng/chiều thì phải phê duyệt lại mô hình phủ ca. Không gọi đây là số người “đồng thời” vì các khoảng ca khác nhau.

Kịch bản minh họa **5 ca/người/tuần** (chưa phải chính sách đã duyệt): mỗi chi nhánh cần tối thiểu HK 17, Maintenance 9, HKS 5, Reception 5, Lead 3, Store 3 để đủ tổng suất; vẫn phải xét giờ nghỉ, ngày nghỉ, ca đêm và chuyển ca. Đủ tổng suất là điều kiện cần, chưa chứng minh lịch khả thi. Không tự thêm người ngoài 39 hoặc xếp người ca kép.

### Đầu mối đề xuất — không gán quản lý/quyền

- DVBV: 431/Hoàng Gia Bảo là đầu mối lập lịch tuần vì DEPT_MANAGER có SHIFT_MANAGE/BRANCH; code Week/Candidates/Assign cho phép quản lý cả 4 bộ phận tại 413, không giới hạn theo employment HK. Manager HK/STORE hiện cũng là431, giữ nguyên.
- DVBV: OM 435/Đặng Hoài Phương phối hợp vận hành; HKS 436/Nguyễn Khánh Linh và425/Trần Minh Khôi (sau duyệt) phối hợp HK; Lead 437/Phan Tiến Đạt phối hợp kỹ thuật;430/Nguyễn Thanh Mai phụ trách kho;426/Lê Thu Hà làm đầu mối trao đổi FO (không tự nâng quyền).
- DVCT: 433/Nguyễn Anh Tuấn là ứng viên đầu mối vận hành sau duyệt OM; chưa có quyền lập lịch. Trong roster mới có EMP041–044/HKS và EMP048–049/Lead để người duyệt lựa chọn đầu mối, chưa gán manager_user_id.
- DVCT thiếu DEPT_MANAGER và STOREKEEPER. Có thể đề xuất bổ sung người hoặc mở scope có kiểm soát trong một yêu cầu riêng; không tự chuyển 431 sang 414, thêm role cho 433 hoặc cấp SHIFT_MANAGE cho OM/Admin.

## Tác động dữ liệu và giới hạn

- 39 FK tham chiếu users, đều enabled/trusted theo snapshot; số bản ghi theo user/bảng/cột có ở sheet Phu_thuoc_DB và diagnostics. Không đổi khóa15 users nên giữ liên kết công việc, audit, chứng từ, notifications và lịch sử; không sửa JSON audit cũ chứa tên demo. Tên hiển thị từ JOINusers sẽ đổi theo hồ sơ mới, lịch sử văn bản snapshot có thể giữ tên cũ.
- 427 có5 assignment ACTIVE,428 có 4,429 có 2; mỗi người 1 work_interval đang mở. Không đóng/đổi phân công để phục vụ đổi tên. 425/432/433 không có assignment ACTIVE, open interval hoặc Workday PLANNED/WORKING tại thời điểm đọc; vẫn phải kiểm tra lại trước ghi.
- 425 có4 assignment COMPLETED và433 có 1; giữ lịch sử. Notifications đang tham chiếu15 users theo FK, không gửi lại hoặc đổi người nhận. Không kiểm tra toàn bộ trigger/nghiệp vụ gửi thư tương lai bằng thực thi.
- 425 và433 có DEPARTMENT_AUTHORITY cả HK+ENG; Word chỉ mô tả employment/role, chưa có lệnh thu hồi các membership này. Giữ 32 membership hiện hữu trong phương án cơ sở, ghi rõ để người duyệt quyết định.
- 15 session chưa hết hạn/chưa thu hồi:424=4,425=2,430=2,431=2,435=1,436=2,437=1,438=1. Đây là số theo revoked_at/expires_at, không chứng minh mọi token sử dụng được. Auth dùng User ID/security stamp; chỉ đổi username không mặc nhiên thu hồi session. Role đổi có thể tác động ngay tới các phiên còn hợp lệ vì Access đọc grants hiện tại. Chưa chọn chính sách thu hồi/giữ phiên, không sửa stamp/password.
- API Create trong UserService luôn tạo ACTIVE và hash mật khẩu đầu vào; không dùng API này cho staging bị khóa trước duyệt. Đề xuất riêng: INACTIVE, passwordNULL, không grant ở giai đoạn tạo hồ sơ nếu được duyệt. Null password được schema cho phép; chưa kiểm thử ghi hoặc đăng nhập với dữ liệu đề xuất.
- Yêu cầu “giữ RowVersion” không thể nghĩa là giá trị phải bất biến sau UPDATE: SQL Server tự tăng. Manifest lưu token gốc cho precondition; không gán lại RowVersion. Cần xác nhận cách hiểu này trước triển khai.
- Không kiểm thử quyền qua API, login, FE hay write workflow trong lượt này. Phân tích quyền là từ dữ liệu SELECT và source Access/UserService/AuthService/WorkdayService. Không kết luận đã end-to-end test 54 accounts.

## Phê duyệt cần có và phương án rollback

Phạm vi duyệt lần sau cần tách rõ: (1) đổi 4 trường của 15 ID và quyết định bootstrap; (2) tạo 39 users, trạng thái employment/account và phương án credential; (3)78 memberships + 42 role assignments mới/3 thu hồi, xử lý extra authority và Owner; (4) kích hoạt/login/reset password/email nếu có — không gộp ngầm vào duyệt hồ sơ; (5) nguồn lực/quyền quản lý lịch DVCT và chính sách nghỉ. Có thể duyệt theo nhóm; phần chưa chốt tiếp tục giữ nguyên.

Trước ghi được duyệt: backup/kiểm tra khả năng restore; đọc lại hashes nguồn, IDs, constraints, grants, toàn bộ uniqueness và RowVersion để chặn thay đổi đồng thời; thực hiện transaction  có audit, xác nhận số dòng và permissions, rollback toàn bộ nếu lệch. Không cần và không tạo backup cho lượt SELECT này.

Sau commit tương lai, rollback bằng thay đổi bù  có duyệt, dựa before-values và rowversion mới; giữ lịch sử/audit, không xóa user đã được tham chiếu. Vô hiệu hóa các account mới và thu hồi quyền đúng batch nếu cần; không chạy DELETE hàng loạt, không phục hồi RowVersion cũ, không ghi đè dữ liệu phát sinh sau batch. Chi tiết nằm trong manifest.

## Xác minh và tệp bàn giao

- Đọc lại tại `2026-11-22T09:18:26Z` và so với snapshot `09:12:17Z`: users (gồm RowVersion), user_roles, memberships, roles, branches, departments và constraints **giống hệt nhau**. Bằng chứng: [read-only-verification.json](../../artifacts/staffing-v3/read-only-verification.json). Đây là đối chiếu các tập dữ liệu nêu trên, không phải chứng nhận toàn bộ database không có hoạt động từ ứng dụng khác.

- **Dry-run: hoàn tất có blocker.** SQL SELECT kết nối thực thành công; kiểm tra trùng và ánh xạ 54 hồ sơ hoàn tất. Không DML, seed, migration, grant, activation, login, email, Git write hoặc thao tácProject 1/Rebuild.
- **Build: Not Run** — không sửa C# hoặc frontend.
- **Tests: kiểm tra dữ liệu/round-trip Excel Passed; kiểm thử ứng dụng/API Not Run**, phù hợp phạm vi chỉ đọc. Chưa thực thi transaction tương lai hoặc kiểm thử login/bootstrap.
- Tệp mới: workbook vàmanifest trong `docs/database/compact-v2/staffing-v3-dry-run/`; báo cáo này; plan `plans/261122-1608-staffing-v3-dry-run/plan.md`; script phân tích, snapshot, bằng chứng vàvalidation trong `artifacts/staffing-v3/`. Không sửa source ứng dụng hoặc tệp nguồn Word/Excel của người dùng.
- Dùng AgentKit `ak-docx` và `ak-xlsx`; một agent. Không khởi động tiến trình nền mới. API PID 42448 có sẵn từ phiên trước được giữ nguyên; không restart hoặc tương tác ghi.
