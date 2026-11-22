# Staffing V3 — triển khai và hậu kiểm

**IMPLEMENTED và VERIFIED:** đã áp dụng Staffing V3 vào `HotelOpsCompactDB` lúc **16:44:19 ngày 22/11/2026**, theo phê duyệt FULL IMPLEMENTATION trong tệp đính kèm. Database có **54 hồ sơ**, trong đó **15 tài khoản cũ ACTIVE, 39 tài khoản mới INACTIVE và chưa có mật khẩu**. Chưa kích hoạt người mới hoặc gửi email; các hồ sơ này chưa đăng nhập được và chưa đủ điều kiện nhận việc/lập lịch.

## Kết quả theo yêu cầu

| Trường | Kết quả |
|---|---|
| DATABASE_TARGET | `DESKTOP-JI8U50T\SQLEXPRESS` / `HotelOpsCompactDB`, 40 bảng |
| BACKUP_STATUS | VERIFIED: COPY_ONLY + CHECKSUM + VERIFYONLY; đã restore thành công một bản độc lập và kiểm thử trên bản restore |
| PREFLIGHT | PASS: nguồn V3, SQL instance, schema, RowVersion, uniqueness, role/grants và chưa có 39 người mới |
| EXISTING_USERS_UPDATED | 15, giữ User ID 424–438; 60 giá trị định danh đổi theo Excel |
| NEW_USERS_CREATED | 39, ID do SQL Server sinh 439–477; DVBV 16, DVCT 23 |
| TOTAL_USERS | 54 |
| ROLE_ASSIGNMENTS_UPDATED | Thêm 42 assignments: 39 mới + 3 chuyển role; thu hồi 3 assignment DEMO cũ; tổng 57 gồm lịch sử |
| BRANCH_MEMBERSHIPS_CREATED | 39 |
| DEPARTMENT_MEMBERSHIPS_CREATED | 39; tổng memberships 110, giữ nguyên 32 cũ |
| ACTIVE_ACCOUNTS | 15 |
| INACTIVE_ACCOUNTS | 39; employment ACTIVE, password NULL, không phát hành credential |
| ROLE_PERMISSION_CHANGES | 0; giữ 10 role chính thức active, 67 permission definitions và 178 grants |
| ADMIN_LOGIN_TEST | PASS: `NamNH1`, User ID 424; User Management/Role Management/Admin dashboard truy cập được; phiên trước đổi tên còn hợp lệ |
| OWNER_SCOPE | VERIFIED: User 438, role OWNER global, branch_id NULL, 12 grants ALL giữ nguyên |
| WORKDAY_VALIDATION | PASS: scope, candidates, capacity, stale version, overlap và chặn INACTIVE; primary có 0 lịch phân công |
| BACKEND_BUILD | PASS, 0 warnings, 0 errors |
| FRONTEND_BUILD | PASS |
| INTEGRATION_TESTS | 31/31 trên fixture; 26/26 login/read/scope trên primary; UI 6/6 |
| FAILED_TESTS | 0 ở lần kiểm tra cuối. Lần đầu có 3 lỗi điều kiện fixture, đã xử lý và kiểm thử lại; xem bên dưới |
| UNRESOLVED_BLOCKERS | Không có blocker còn chặn migration. Các việc activation, nhân sự DVCT, phủ ca và authority cũ được tách riêng |
| FINAL_VERDICT | IMPLEMENTED + VERIFIED trong phạm vi được duyệt; không tuyên bố 39 người mới đã đăng nhập hoặc đã có lịch vận hành đầy đủ |

Hai role DEMO vật lý vẫn tồn tại ở trạng thái inactive để giữ lịch sử. Cả ba assignment nguồn đã có `revoked_at`; không còn assignment chưa thu hồi trỏ vào role inactive trong 54 hồ sơ.

## Backup, transaction và bảo toàn

Backup ngay trước migration chính:

`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261122_164418139.bak`

Bản dùng diễn tập restore: `HotelOpsCompactDB_before_workday_20261122_163218717.bak`, được restore thành database mới `HotelOpsCompactWorkdayTestStaffingV3202611221630`. Không ghi đè database có sẵn. Database thử nghiệm lấy hoàn toàn từ CompactDB, không dùng Rebuild/Project1. Mỗi lần thử có ghi tiếp theo đều có backup trước.

Migration dùng một transaction, `XACT_ABORT`, application lock, khóa bảng và đối chiếu phiên bản vừa đọc. Trước khi ghi, so bốn giá trị định danh với before-values đã duyệt, kiểm tra grants và toàn bộ định danh đích. Khi ghi, không gán identity hoặc RowVersion bằng tay. SQL Server sinh User ID và RowVersion mới.

Batch ID: `B5D5DF03-3E1C-40F4-B60A-6AE335231630`; audit chính `audit_id=248`, `entity_type=StaffingV3`, `action=APPLY`, actor User ID424. Audit chứa before/after của users, assignments, memberships, kết quả validation và hash các bảng được bảo toàn. Không đưa password hash hoặc token vào báo cáo/audit này.

Trong transaction đã xác minh password hash, security stamp, trạng thái, manager, phone, dữ liệu đăng nhập của 15 người cũ không đổi; sessions giữ nguyên; 32 memberships cũ và assignment không chuyển role giữ nguyên; role definitions, permission definitions, grants và các bảng nghiệp vụ không đổi. Lịch sử audit cũ không bị sửa trước khi append batch audit. Việc đăng nhập kiểm thử sau đó có cập nhật last_login/RowVersion và tạo session theo cơ chế ứng dụng bình thường, không thu hồi các session đã tồn tại.

Đã cố ý gây lỗi ở cuối migration trên fixture, trước COMMIT: transaction rollback và preflight tiếp theo trở lại baseline 15 users. Đã kiểm tra chạy lặp trả `ALREADY_APPLIED`, không nhập thêm hồ sơ. Đã diễn tập khôi phục riêng Admin bằng before-values từ audit, đăng nhập lại với `demo_admin`, rồi đưa lại `NamNH1` và đăng nhập thành công. Script recovery mặc định chỉ kiểm tra; cần `-Apply` mới ghi, có backup và audit. Không chạy recovery trên primary vì primary thành công.

Hậu kiểm SQL trực tiếp: không trùng employee code/username/email; 39 người mới đủ branch, employment department và role; không liên kết mới sai chi nhánh; 160 FK giữ nguyên. `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` trả **0 vi phạm**. Định danh đủ 54 hàng khớp manifest. Bản chứng cứ cuối: [validation-HotelOpsCompactDB.json](../../artifacts/staffing-v3-implementation/validation-HotelOpsCompactDB.json).

## Nhân sự và phân quyền đã áp dụng

| User ID | Chuyển role | Scope |
|---|---|---|
| 425 | DEMO_MANAGER → HOUSEKEEPING_SUPERVISOR | DVBV / 413 |
| 432 | DEMO_AUDITOR → RECEPTION | DVBV / 413 |
| 433 | DEMO_MANAGER → OPERATION_MANAGER | DVCT / 414 |

Role cũ được thu hồi assignment, không sửa lịch sử role_id hoặc sao chép grants DEMO. 39 người mới được gán đúng role/scope nhưng INACTIVE nên chưa có quyền runtime. Chỉ trạng thái employment của hồ sơ mới là ACTIVE để biểu thị nhân sự hiện hữu; trạng thái account vẫn khóa.

| Role | DVBV tổng / ACTIVE | DVCT tổng / ACTIVE |
|---|---:|---:|
| HK_STAFF | 10 / 2 | 10 / 1 |
| MAINTENANCE_STAFF | 5 / 1 | 5 / 0 |
| HOUSEKEEPING_SUPERVISOR | 4 / 2 | 4 / 0 |
| RECEPTION | 3 / 2 | 3 / 0 |
| MAINTENANCE_LEAD | 2 / 1 | 2 / 0 |

Đủ định biên **hồ sơ** theo V3 ở cả hai chi nhánh. Đây không phải số nhân viên hiện có thể đăng nhập/nhận ca. Cơ chế cấp mật khẩu an toàn chưa được xác minh, nên dùng đúng phương án INACTIVE/password NULL đã được Owner phê duyệt.

## Kiểm thử đã thực hiện

- `dotnet build HotelOps.slnx --no-restore`: PASS, 0 warnings/errors.
- `dotnet test HotelOps.slnx --no-build --no-restore`: 26 PASS, 0 FAIL, 1 SKIP. Test worker bị skip do thiếu fixture worker chuyên biệt; không tuyên bố worker đã được kiểm thử trong lượt này.
- `npm run build`: PASS; `npm run test:compact`: 4 PASS.
- API fixture: **31 PASS**. Login đủ 15 username mới; xác minh đủ 10 actor và grants/scopes; Admin quản lý user/role; OM giới hạn chi nhánh; HKS giao dọn phòng, HK thực hiện, HKS độc lập kiểm tra checklist; Maintenance Lead giao và xác minh, staff thực hiện; Reception check-in/move/check-out; Storekeeper đọc tồn kho/chứng từ; Owner đọc hai chi nhánh và bị chặn thao tác ghi; Department Head quản lý Workday đúng branch; chặn người INACTIVE, ca vượt capacity, trùng ca, RowVersion cũ và Start ca tương lai. Các ca kiểm thử riêng lẻ trên fixture đã được hủy; không tạo lịch tuần thật.
- API primary: **26 PASS**, chỉ login, đọc dữ liệu và kiểm tra phạm vi/deny quyền, không chạy luồng thay đổi công việc/phòng/lịch. Bộ test có một POST Owner cố tạo work và kiểm tra HTTP403, không có bản ghi tạo thành công. Admin giữ session qua migration và đăng nhập mới thành công.
- UI fixture bằng Edge headless: **6 PASS**, gồm đăng nhập Admin `NamNH1`, thấy54 hồ sơ/39 INACTIVE, cuộn khung danh sách đến người cuối mà header vẫn giữ vị trí, menu HKS sau đổi role, Workday tải đủ nội dung của Department Head, Owner chỉ có menu đọc và hai chi nhánh. Không có unhandled browser error. Đã xem ảnh kết quả sau khi tải xong.

Bằng chứng: [API fixture](../../artifacts/staffing-v3-implementation/api-5784-results.json), [API primary](../../artifacts/staffing-v3-implementation/api-5785-results.json), [UI](../../artifacts/staffing-v3-implementation/ui-results.json), [phiên Admin giữ nguyên](../../artifacts/staffing-v3-implementation/primary-session-continuity.json), [ảnh cuộn danh sách](../../artifacts/staffing-v3-implementation/ui-admin-scrolled.png), [ảnh Workday](../../artifacts/staffing-v3-implementation/ui-workday.png).

Lần API fixture đầu có 3 lỗi: chọn phòng theo readiness mà chưa lọc operational status khiến move bị từ chối; HK và Maintenance còn timer của công việc trước nên unique index chặn Start công việc thứ hai. Đây là guard hợp lệ trên dữ liệu được restore. Đã bổ sung bước tạm dừng timer bằng API trên fixture, hoàn tất cleaning/inspection để chuẩn bị phòng hợp lệ và lọc trạng thái phòng đúng; chạy lại31/31 đạt. Không nới constraint hoặc sửa logic ứng dụng để làm test qua. Chứng cứ lần đầu vẫn giữ trong `api-5784-first-attempt.json`.

Các lần gặp HTTP429 được chờ đúng cửa sổ rate limit, không tắt hoặc nới giới hạn. Hook chặn chạy Vite bằng đường dẫn dependency trực tiếp; chuyển sang `npm run dev` được hỗ trợ, không sửa hook. Không còn hành động bị chặn.

## Tệp tạo trong lượt triển khai

- [09_StaffingV3.sql](../../scripts/database/compact-v2/09_StaffingV3.sql): transaction, concurrency, invariants và batch audit.
- [Invoke-StaffingV3.ps1](../../scripts/database/compact-v2/Invoke-StaffingV3.ps1): kiểm tra nguồn, backup, preflight/apply/fault test, chống chạy lặp.
- [Verify-StaffingV3.ps1](../../scripts/database/compact-v2/Verify-StaffingV3.ps1): kiểm chứng roster, counts, links, scope và constraints.
- [Recover-StaffingV3Admin.ps1](../../scripts/database/compact-v2/Recover-StaffingV3Admin.ps1): khôi phục định danh Admin có guard/backup/audit.
- [test-staffing-v3-api.mjs](../../scripts/database/compact-v2/test-staffing-v3-api.mjs): bài kiểm thử actor và workflow, ghi nghiệp vụ chỉ trên fixture.
- Plan, báo cáo, nhật ký cục bộ và các script/chứng cứ trong `artifacts/staffing-v3-implementation/`.

Không sửa C# hoặc frontend source, không đổi schema/entities, không tạo migration EF, không commit/push/merge/deploy. Các thay đổi source đã có trước lượt này được giữ nguyên. Làm bằng một agent, review trực tiếp; không tuyên bố có reviewer độc lập. Các API/Vite kiểm thử tạm được dừng sau khi thu thập bằng chứng; database fixture và backup được giữ để truy vết.

## Các việc còn cần quyết định riêng

1. **Provisioning 39 tài khoản:** chọn quy trình cấp mật khẩu riêng từng người hoặc reset an toàn, người tiếp nhận credential và thời điểm activation. Sau khi duyệt mới cấp/kích hoạt và thử login; không tái dùng mật khẩu chung hoặc gửi tới `@ducvuong.com` mặc định.
2. **Department Head DVCT:** chọn nhân sự/ủy quyền và scope được duyệt; hiện chỉ431 có SHIFT_MANAGE tại DVBV. Không cấp SHIFT_MANAGE cho OM/Admin hoặc tự mở quyền431 sang DVCT. Cần yêu cầu riêng vì roster V3 chưa có người phù hợp ở DVCT.
3. **Storekeeper DVCT:** bổ sung nhân sự theo yêu cầu riêng hoặc phê duyệt phương án phụ trách; không thêm người ngoài39. DVBV có1 thủ kho, DVCT chưa có.
4. **Phủ ca và ngày nghỉ:** sau activation vẫn còn HK10 so với12 suất/ngày, Maintenance5 so với6; kho DVBV1/DVCT0 so với2 suất/ngày khi tối đa1ca/người. Sáng/chiều của ENG/Kho là hai khoảng làm việc, không tự xem là hai ca8giờ hoặc cho phép ca kép. Chốt chính sách nghỉ và tuần đích trước khi lập lịch.
5. **DEPARTMENT_AUTHORITY của425/433:** giữ nguyên HK/ENG theo ngoại lệ đã cho phép. `WorkService` dùng authority để route/accept yêu cầu, `DocumentService` dùng trong xác định đầu mối/phê duyệt; chưa đủ quyết định về trách nhiệm mới và toàn bộ workflow để tự thu hồi an toàn. Lập rà soát riêng từng membership và work/document đang phụ thuộc trước khi đóng hiệu lực. Không sửa manager431 của HK/STORE DVBV.

Các mục này không làm phần nhập dữ liệu V3 bị thiếu, nhưng giới hạn khả năng vận hành đầy đủ. Không có phê duyệt gửi email, activation hàng loạt, thêm role/người hoặc lịch tuần trong lượt này.
