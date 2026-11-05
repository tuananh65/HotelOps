# Task03B — Deployment Execution Report

**SUCCESS WITH NOTES. RBAC đã COMMIT trên database chính HotelOpsDB; verification cốt lõi PASS.**

## A. Deployment Overview

- Instance: `DESKTOP-JI8U50T\SQLEXPRESS`.
- Database: `HotelOpsDB`, DB_ID=22, create stamp `2026-10-18 02:41:14.627`, ONLINE, MULTI_USER, SIMPLE.
- Candidate: `../Task03A/REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql`, không sửa nội dung.
- SHA-256: `C1FD3904871107F0284CDC8ADF5CFF8193972B408ECBCBC3F3EBE51AF29B3A64`.
- Lần orchestration hoàn tất: **21/10/2026 20:33:10–20:36:47 UTC+7** (`13:33:10.6520659Z–13:36:47.8283902Z`). Các kiểm tra ban đầu và lỗi harness trước lần này được lưu riêng.
- Candidate bắt đầu trong khoảng `13:36:17.3902380 UTC`, batch cuối được xác nhận COMMIT trước `13:36:35.6206841 UTC` (20:36:35 UTC+7). Không khẳng định thời điểm xác nhận là timestamp nội bộ chính xác của COMMIT.
- Không thay đổi database trial/rehearsal hoặc database nghiệp vụ khác. BACKUP có ghi backup history hệ thống trong msdb theo hoạt động chuẩn của SQL Server.

## B. Safety Preflight

Branch đúng `feature/BE1-authorization`. Working tree có các thay đổi chưa commit từ Task01–03A, được giữ nguyên. 126 file source/config/Release được bảo vệ đều khớp baseline. Candidate đúng hash và 4 batch, có 288 kiểm tra fingerprint (72 bảng trong mỗi batch).

Preflight nguồn so với Task03A: **72/72 bảng khớp count/full-row SHA-256; 5/5 nhóm schema khớp** (columns, FK, CHECK, defaults, indexes). Không cập nhật baseline để ép guard qua. Assignment OM/DH/Storekeeper còn đúng dữ liệu đã duyệt; expected grants trước triển khai khớp chính xác 18 thêm, 60 thu hồi, 33 đổi scope.

Maintenance đã kiểm tra tiến trình, services, scheduled tasks và SQL sessions trước preflight, backup, candidate, từng batch và sau verification. Không có HotelOps API/Kestrel/IIS writer hoặc background service đang chạy. `dotnet` PID31680 là `Microsoft.VisualStudio.ProjectSystem.Server.BuildHost.dll` của VS Code. SQL Agent dừng; một job enabled tồn tại nhưng không có Agent chạy. Không phát hiện scheduled writer phù hợp bộ lọc kiểm tra.

Hai session quan sát trước đó là CEIP và SSMS đang sleeping trong master, không request hoặc transaction; được kiểm tra lại tại từng gate. Không KILL session, không SINGLE_USER, không đổi database mode. Đây là bằng chứng quiescence trong cửa sổ triển khai, không phải cơ chế ngăn mọi kết nối mới trong tương lai. Fingerprint sau triển khai xác nhận không có writes ngoài changeset trong dữ liệu được kiểm tra.

Không cần dừng hoặc khởi động lại API vì API đã không chạy. Không tự mở application/writers sau khi kết thúc.

## C. Backup & Recovery

Backup mới thực hiện ngay trước candidate, ngày 21/10/2026 lúc **20:36:05 server-local**, COPY_ONLY, CHECKSUM, tên duy nhất, không ghi đè, không đổi ACL, nằm ngoài Git repository trong thư mục backup được SQL Server cấu hình.

- Identifier: `RBAC03B_20261021_133605_d707920c940243b19f38814fcd69f6bb`.
- msdb backup_set_id: **9383**.
- Backup UUID: `58B5E174-851A-4330-8DA2-AFE78306EF1A`.
- Backup payload theo msdb: **18,563,072 bytes**; không diễn giải thành filesystem file length.
- RESTORE VERIFYONLY WITH CHECKSUM: **PASS**.
- Logical files: `HotelOpsDB` (D), `HotelOpsDB_log` (L), khớp quy trình restore đã diễn tập ở Task03A.

Không restore backup mới vào DB khác trong Task03B. Actual restore proof thuộc Task03A; VERIFYONLY của backup mới không được gọi là actual restore test. Vị trí backup có thể tra bằng backup_set_id/identifier trong msdb, không lưu đường dẫn đầy đủ trong package03B.

SIMPLE không bảo đảm PITR/tail-log để cứu writes sau backup. Không có automatic downgrade hoặc quyền restore đè nguồn. Xem `RECOVERY_AND_INCIDENT_NOTES.md` và recovery plan Task03A.

## D. Actual Deployment

| Batch | Thực tế |
| --- | --- |
| 1 | Identity, approval, fingerprint guards; USE HotelOpsDB — PASS |
| 2 | Guards; NOCOUNT, XACT_ABORT, LOCK_TIMEOUT — PASS |
| 3 | Guards; data_scope đã tồn tại nên additive ALTER không chạy — PASS |
| 4 | Guards; transaction cho catalog/personnel và 6 audits; COMMIT — PASS |

Bốn batch được gửi nguyên văn trên cùng SQL connection; GO chỉ là dấu tách của client. Không giả định chúng nằm chung transaction. Batch cuối chứa một BEGIN TRANSACTION/COMMIT với TRY/CATCH rollback; ba batch trước không nằm trong transaction này. Schema trước/sau giữ nguyên.

Approval session context chỉ được đặt sau backup và các gate, rồi xóa sau COMMIT. TRANCOUNT=0 và XACT_STATE=0 được kiểm tra sau khi candidate hoàn tất. Không có lỗi từ candidate, không rollback hoặc chạy lại Upgrade, không chạy main Dry Run, Full Schema hoặc Seed bổ sung.

Hai lỗi harness đã xảy ra **trước backup và trước mọi batch candidate**: vị trí khai báo param và SELECT projection tạo identifier rỗng. Đã phân tích, sửa và kiểm chứng; lần lỗi SELECT có TRANCOUNT=0, 72 bảng không đổi. Receipt lỗi được giữ nguyên trong `read-only-harness-error.json`; lần tiếp tục kiểm tra lại toàn bộ gates. Không che lỗi hoặc coi đó là candidate COMMIT thất bại. CHECKDB có lúc chờ bộ nhớ, cuối cùng PASS; không đổi cấu hình hoặc timeout. Chi tiết trong incident notes.

## E. RBAC Results

| Chỉ tiêu | Before | After | Expected |
| --- | ---: | ---: | ---: |
| Roles | 11 | 12 | 12 |
| Active Roles | 11 | 10 | 10 |
| Inactive Roles | 0 | 2 | 2 |
| Permissions | 47 | 62 | 62 |
| Grants | 182 | 140 | 140 |
| DEPARTMENT Grants | 45 | 0 | 0 |

Scope sau triển khai: **ALL=8, BRANCH=81, OWN=51**. Catalog metadata của roles/permissions và tuple role_code/permission_code/data_scope đều khớp hai chiều, missing=0/extra=0. Đã chạy cả `04_HotelOps_Verification_ReadOnly.sql` và postdeployment SQL Task03A.

10 roles active: SYS_ADMIN, OPERATIONS_MANAGER, DEPT_MANAGER, HK_SUPERVISOR, HK_STAFF, MAINTENANCE_LEAD, MAINTENANCE_STAFF, STOREKEEPER, RECEPTION, OWNER. HK_INSPECTOR và STAFF inactive.

OWNER chỉ có DASHBOARD_VIEW:ALL và REPORT_VIEW:ALL. SYS_ADMIN chỉ có 6 grants quản trị đúng matrix. STAFF inactive còn 10 grants đúng Release; PermissionService sau này phải kiểm tra trạng thái role, không suy ra grants của role inactive được sử dụng.

Changeset thực tế khớp expected: **18 grant keys thêm, 60 thu hồi, 33 scope changes** sau normalize aliases. Thêm đúng 15 permissions. Danh sách chính xác nằm trong `actual-changeset.json` và expected Task03A.

Nhân sự:

- DH: User4/Branch1, User19/Branch2; thu hồi DH5/17/18/23 ở Branch1 và DH20/25 ở Branch2.
- Storekeeper: User5/Branch1 giữ nguyên; User19 không còn Storekeeper; User5 không còn DH.
- OM: User1 chỉ Branch1; role branch NULL thành1, MANAGER access Branch2 đã revoked đúng thời gian thực thi.
- HK Inspector8/Branch1 và14/Branch2 inactive, thêm Supervisor tương ứng; giữ Supervisor2/12.
- Chỉ department_id của User4 và19 thành NULL. Không thay account status, primary branch, password/temporary credentials hoặc các user fields khác.

## F. Integrity & Preservation

DBCC CHECKDB và CHECKCONSTRAINTS trước/sau: **PASS, không findings**. FK/CHECK enabled và trusted; unique indexes enabled. Năm schema fingerprints giữ nguyên, không phát hiện schema drift.

| Bảng thay đổi dự kiến | Before → After |
| --- | --- |
| roles | 11 → 12 |
| permissions | 47 → 62 |
| role_permissions | 182 → 140 |
| user_roles | 30 → 32 |
| users | 26 → 26 |
| user_branch_access | 28 → 28 |
| audit_logs | 14 → 20 |

**65/65 bảng còn lại giữ nguyên count/full-row fingerprint**, bao gồm 10 SQL sessions, departments, inventory, requests, shifts và business history. Chỉ có đúng 7 bảng được duyệt thay đổi.

Users được đối chiếu toàn bộ cột bằng projection chỉ đổi hai department_id; credentials không xuất ra report. Tất cả cột của user_roles cũ khớp projection đã duyệt; access fields được giữ nguyên ngoại trừ đúng revoked_at đã duyệt. Role IDs/created_at cũ và identity/semantic resource/action của permission cũ giữ nguyên. Các kiểm tra này bổ sung cho so sánh semantic catalog và personnel.

14 audit records cũ có fingerprint nguyên vẹn. Thêm đúng 6 migration UPDATE records cho roles, permissions, role_permissions, users, user_roles, user_branch_access; actor NULL, deployment tag đúng và old/new JSON hợp lệ. Payload của candidate không chứa password/cookie/session secrets.

## G. Authentication Compatibility

Authentication source/configuration, CurrentUserAccessor, connection configuration và EF mappings không thay đổi. Scan runtime code không có authorization theo legacy role aliases. Users ngoài hai department_id và toàn bộ 10 SQL sessions được bảo toàn; không revoke tất cả sessions hoặc đổi password.

Đã chạy thực tế sau triển khai:

`dotnet run --no-restore --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj`

Kết quả **14/14 PASS**, không warning/error trong output lệnh. Đây là console verification ticket/snapshot/accessor/DI; **không thay thế HTTP integration hoặc runtime SQL session validation**.

HTTP login/authenticated endpoint/logout: **Not Run**. API không chạy và không có test credential được cung cấp cho task; không tự đặt lại password, tạo cookie hoặc session secret để kiểm thử. Không tuyên bố toàn bộ Authentication end-to-end PASS.

`dotnet build HotelOps.slnx`: **Not Run trong Task03B** vì không sửa C#. `dotnet test HotelOps.slnx`: **Not Run trong Task03B**; solution không có automated test project liên quan, console verification được báo riêng. Lệnh dotnet run có thể build project verification/API khi cần nhưng không được gọi là full solution build.

## H. Outstanding Work

1. Legacy Department Retirement vẫn chưa hoàn thành: LAU/SEC/FB Branch1 và SEC Branch2 active, Release04 báo đúng 4 warnings đã được chấp nhận cho phạm vi chuyển tiếp. Departments, warehouses, requests, inventory và shifts không được cleanup trong task này.
2. EF Core RolePermission chưa có DataScope mapping; cần task riêng trước khi xây logic dùng scope.
3. PermissionService/Authorization Core chưa triển khai; chưa kiểm thử branch/OWN/role inactive/SoD end-to-end.
4. Cần HTTP authentication integration với test account được phép và environment thích hợp; các writes do login/touch/logout phải phân biệt với preservation baseline của deployment.

## I. Git Status

Chỉ tạo artifacts của task tại `scripts/verification/RbacUpgradeDeployment/Task03B/`. Không sửa C#, Authentication, CurrentUserAccessor, DbContext/entities, shared Release hoặc hồ sơ Task03A. 27 file Task03A được fingerprint để đối chiếu cuối task.

Các thay đổi có sẵn vẫn còn: Program.cs +3 dòng, thư mục Security/Authorization và scripts/verification chưa commit. Cuối phiên phát hiện thêm `scripts/database/release/codex-attention-alert.wav` untracked ngoài package03B; không có thao tác của task tạo/chỉnh/xóa file này, giữ nguyên để developer kiểm tra. File WAV không thuộc SQL Release đã được hash bảo vệ.

Không commit, push, merge, reset hoặc tạo PR. Danh sách artifact Task03B xem README và directory; các bằng chứng thực thi/phân tích lỗi đều được giữ lại.

## J. Final Verdict

**SUCCESS WITH NOTES**: database chính đã COMMIT RBAC Release, exact catalog/grants/personnel, integrity và preservation đều PASS. Kết luận dựa trên truy vấn sau COMMIT và fingerprints thực tế, không chỉ thông báo của candidate.

Giới hạn HTTP authentication testing, SIMPLE recovery và các công việc Authorization/Department Retirement còn lại được ghi riêng. Không cần recovery cho lần triển khai này; không tự chạy lại candidate.
