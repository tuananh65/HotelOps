# Task 03A — Main RBAC Deployment Readiness

21/10/2026. **Verdict: READY WITH NOTES — chỉ sẵn sàng trình duyệt Task03B. HotelOpsDB chưa được Upgrade.**

## A. Context Verification

Branch `feature/BE1-authorization`; toàn bộ thay đổi Task01 và working tree có sẵn được giữ nguyên. Đã đọc AGENTS.md, Release README/scripts01–04, hồ sơ Task02/02B/02C/02D/02E, Authentication, CurrentUserAccessor và RBAC EF entities/mappings.

Task03 chỉ RBAC + OM/DH/Storekeeper. Department Retirement là migration bắt buộc riêng, chưa hoàn tất. Không deactivate FB/LAU/SEC; không chuyển stock, hủy request/shift hoặc reset fixture. Không mang tám thay đổi Task02C sang database chính.

## B. Current HotelOpsDB State

Server `DESKTOP-JI8U50T\SQLEXPRESS`, database `HotelOpsDB`, DB_ID=22, create_date=`2026-10-18 02:41:14.627` (SQL server-local), compatibility160.

| Hạng mục hiện tại | Kết quả SELECT |
| --- | ---: |
| Roles / Active / Inactive | 11 / 11 / 0 |
| Permissions | 47 |
| Grants | 182 |
| ALL / BRANCH / OWN / DEPARTMENT | 27 / 57 / 53 / 45 |
| Users / SQL Sessions | 26 / 10 |

Preflight 72 bảng khớp count + SHA-256 với **sourceBefore của Task02**, là dữ liệu dùng tạo trial ban đầu: **0 bảng drift**. Source hiện tại không được so sánh bằng cách yêu cầu giống trial đã nâng cấp/cleanup. Schema hashes cho columns, FK, CHECK, DEFAULT, indexes được lưu riêng; bản restore mới khớp toàn bộ.

DH/Storekeeper/OM đúng tập dữ liệu đã review: DH Branch1 users4/5/17/18/23, Branch2 users19/20/25; user1 GENERAL_MANAGER với branch role NULL, primary branch1 và MANAGER access1/2; user5/19 kiêm WAREHOUSE_KEEPER. Các selections4/19 và scope vẫn hợp lệ; không tự chọn personnel khác.

`role_permissions.data_scope` đã tồn tại `varchar(20) NOT NULL`, CHECK enabled/trusted. Các cột Authentication prerequisite hiện diện. Deployment candidate chặn nếu data_scope mất; không cho additive pre-step ngoài transaction âm thầm chạy trên một schema khác baseline.

Chi tiết: [preflight-results.txt](preflight-results.txt), [deployment-state.json](deployment-state.json), [PREFLIGHT_RESULTS.md](PREFLIGHT_RESULTS.md).

## C. Backend Compatibility Assessment

- Scan current `src/HotelOps.Api` không tìm thấy hard-coded role codes GENERAL_MANAGER/HK_MANAGER/MAINT_MANAGER/TECHNICIAN/WAREHOUSE_KEEPER trong runtime logic; không có IsInRole/RequireRole hoặc permission service truy cập grants.
- FE-01 đọc users/sessions; cookie principal chỉ có các identity/session fields được kiểm soát. Cookie validation không đọc role catalog. Auth DTO không phụ thuộc department_id hoặc role name; việc clear department của DH4/19 không phá DTO.
- CurrentUserAccessor reuse ticket + validated request-local snapshot; không đọc roles/grants.
- `RolePermission` và DbContext vẫn **chưa mapping DataScope**. Đây là việc phải xử lý trước PermissionService/DataScope, không phải blocker cho các Authentication endpoints hiện tại. Không sửa generated models ở Task03A.
- CurrentUserAccessor console verification đã chạy lại: **14/14 PASS**, lệnh `dotnet run --no-restore --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj`.
- Không chạy login/change-password/logout HTTP thật hoặc thử credential người dùng; không tuyên bố API integration PASS. Rehearsal chứng minh 10 session rows giữ nguyên, mọi user field ngoài department_id giữ nguyên, AccountStatus/password/temp/lockout không đổi. Các nhánh khác/API chưa có trong checkout cần revalidate khi tích hợp, không suy ra tương thích cho code chưa inspect.

Không tìm thấy blocker cụ thể với FE-01/API hiện có trong branch này. Không có thay đổi C#; solution build/test không chạy lại, không có build result được bịa đặt.

## D. Backup & Restore Readiness

- Backup mới: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\RBAC03A_20261021_24e14e196c6a.bak`.
- COPY_ONLY, CHECKSUM, NOINIT; tên mới, không overwrite. Backup start/finish theo msdb server-local: **2026-10-21 19:49:54.000**.
- Backup payload size theo msdb: **17,711,104 bytes** (khoảng16.89MiB); `compressed_backup_size` cũng17,711,104. Không khẳng định đây là filesystem file length.
- RESTORE VERIFYONLY CHECKSUM: PASS. **Restore thực tế** sang database mới cũng PASS.
- Clone: `HotelOps_RBAC03A_Rehearsal_20261021_a236462a264f`, DB_ID28, physical `.mdf` / `_log.ldf` riêng trong SQL DATA directory. Không WITH REPLACE, không drop/restore đè DB nào; clone được giữ lại.
- Restore trước Upgrade khớp nguồn đủ72 table fingerprints và5 schema hashes. [restore-proof.json](restore-proof.json) ghi physical paths/identity.

Lỗi môi trường đã xử lý: PowerShell Get-Item bị Windows ACL chặn file backup. Backup/VERIFYONLY đã thành công trước lỗi, chưa restore. Không sửa ACL; tiếp tục explicit resume từ đúng backup đã có, xác minh msdb/VERIFYONLY, target chưa tồn tại rồi restore. Dùng size metadata SQL thay vì đọc file từ PowerShell. Đây không phải bỏ qua quyền hoặc chạy backup ghi đè. Xem [backup-evidence.json](backup-evidence.json).

## E. Reviewed Deployment Script

Candidate chính: [REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql](REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql).

**SHA-256 được trình duyệt:**

`C1FD3904871107F0284CDC8ADF5CFF8193972B408ECBCBC3F3EBE51AF29B3A64`

Không script/batch nào của candidate này đã được chạy trên HotelOpsDB. Nó không tự mở approval gate.

- Dựa Release02, thay đúng3 executable target anchors; giữ guards/transaction/catch rollback. Không chạy Full01 hoặc Seed03.
- Mỗi GO batch kiểm tra server, DB_NAME, DB_ID22 và createStamp; riêng session approval gate Task03B mặc định chưa có. Mỗi batch kiểm tra đủ72 baseline fingerprints; SSMS tiếp tục sau lỗi batch trước vẫn không được đi qua một batch bỏ kiểm tra drift.
- Không dynamic SQL hoặc cross-database DML; inspected USE duy nhất là đúng target của variant. Nhân sự IDs chỉ trong environment-specific candidate, không vào Release chuẩn.
- @SelectedDh=(1,4),(2,19); revoke role Storekeeper19/B2 và MANAGER access1/B2 trong transaction hiện có. Release làm các alias/DH/Inspector transitions còn lại.
- Thêm **6 migration-level audit rows** trong cùng transaction, một row cho mỗi bảng thay đổi nghiệp vụ/catalog. Actor NULL cho SQL maintenance, không impersonate staff; before/after JSON không chứa credentials, chỉ users.user_id/department_id cho user snapshot. Audit cũ giữ nguyên. Đây là enhancement so với Release02 chuẩn, cần nằm trong exact changeset DEV1 duyệt Task03B.
- Source data_scope đã có; prefix chặn trường hợp mất cột, tránh giới hạn rollback của original additive pre-step. Không có schema change dự kiến.
- Không thực hiện department/warehouse/catalog fixture cleanup. Bốn legacy departments giữ Active; cảnh báo này là expected out-of-scope, không phải hoàn tất mô hình department.

### Danh sách thay đổi dự kiến để duyệt Task03B

| Bảng / scope | Trước → Sau | Exact expected changes |
| --- | --- | --- |
| roles | 11→12; active11→10, inactive0→2 | Rename GENERAL_MANAGER→OPERATIONS_MANAGER, HK_MANAGER→HK_SUPERVISOR, MAINT_MANAGER→MAINTENANCE_LEAD, TECHNICIAN→MAINTENANCE_STAFF, WAREHOUSE_KEEPER→STOREKEEPER; giữ IDs của alias; thêm OWNER; HK_INSPECTOR/STAFF Inactive; đồng bộ metadata theo Release |
| permissions | 47→62 | Thêm15 codes dưới đây; đồng bộ permission metadata theo Release, không invented codes |
| role_permissions | 182→140 | Sau normalize old aliases: **18 added keys, 60 removed keys, 33 scope updates**; target140 tuples khớp Release hai chiều; scopes8 ALL/81 BRANCH/51 OWN,0 DEPARTMENT |
| user_roles | 30→32 | Deactivate DH5/17/18/23 B1 và20/25 B2; deactivate Storekeeper19 B2; Inspector8 B1/14 B2→inactive và thêm Supervisor assignments tương ứng; role OM user1 branchNULL→1. DH4/19, Storekeeper5 được giữ |
| users | 26→26 | Chỉ department_id của4:3→NULL,19:9→NULL; primary branch/account/credential fields giữ nguyên |
| user_branch_access | 28→28 | Chỉ MANAGER access user1/Branch2 revoked_at=actual execution UTC; Branch1 giữ nguyên |
| audit_logs | 14→20 | Append6 table-level migration records với before/after và deployment ID; không sửa/xóa14 records cũ |
| 65 bảng khác | Count + content giữ nguyên | Bao gồm10 SQL Sessions, departments, stocks, requests, shifts và toàn lịch sử nghiệp vụ ngoài changeset |

15 permission codes thêm: ASSET_RETIRE, ASSET_TRANSFER, INCIDENT_ESCALATION_HANDLE, INCIDENT_TRIAGE, IR_OVERDUE_HANDLE, MAINTENANCE_ASSIGN, MR_APPROVE_FALLBACK, ROOM_OCCUPANCY_MOVE, STOCK_ADJUST_POST, STOCK_ISSUE_POST, STOCK_RECEIPT_POST, STOCK_RETURN_POST, STOCK_REVERSE, STOCK_TRANSFER_POST, STOCKTAKE_PERFORM.

Exact18 additions/60 removals/33 scope changes và140 target tuples nằm trong [EXPECTED_CHANGES_FOR_TASK03B.json](EXPECTED_CHANGES_FOR_TASK03B.json). Đếm theo semantic codes, không ép main có new numeric IDs/timestamps giống clone: Dry Run trên clone có thể tạo identity gaps, main không được Dry Run ghi ở Task03A.

## F. Rehearsal / Dry Run Results

| Check | Result |
| --- | --- |
| Restore current main backup into new independent clone | PASS |
| Approval gate khi chưa set trên clone | PASS: SQL53001 trước writes |
| Dry Run variant và ROLLBACK trên clone | PASS,72 table contents +5 schema hashes khôi phục |
| COMMIT variant trên clone | PASS, chỉ clone mới |
| Catalog metadata và mappings hai chiều | PASS exact12/62/140 |
| Fixed personnel / Owner2 ALL / role states | PASS |
| DBCC CHECKDB / CHECKCONSTRAINTS | PASS, không findings |
| FK/CHECK enabled/trusted, unique indexes enabled | PASS |
| Users ngoài department field / SQL sessions | PASS unchanged |
| Source cuối rehearsal | PASS,72 table fingerprints +5 schema hashes unchanged |
| Source/config/Release protected files | PASS126 files unchanged |

Sau COMMIT rehearsal đã tăng cường read-only drift guard ở cả4 batch. Đã tái tạo candidate cũ từ cùng generator và so exact mutation body, giữ bằng chứng ban đầu trong `initial-rehearsal-evidence/`. Sau đó test cả4 batch trên clone đã nâng cấp: mỗi batch bị SQL53005 chặn, không có thêm data writes; source/clone contents giữ nguyên. Phần DML đã được Dry Run/COMMIT test trước, thay đổi sau đó chỉ lặp lại cùng read-only guard. **Không nói rằng toàn bộ strengthened file đã được COMMIT lần hai.** Các SQL trong thư mục initial là superseded evidence, không phải candidate để duyệt/chạy.

## G. Rollback & Verification Plan

Xem [BACKUP_RECOVERY_PLAN.md](BACKUP_RECOVERY_PLAN.md) và [VERIFICATION_PLAN.md](VERIFICATION_PLAN.md). Trước Task03B phải revalidate exact baseline và thiết lập cửa sổ không có app writers; session touch/login cũng làm fingerprint drift. Có drift thì dừng/reassess, không tự cập nhật hashes hoặc bỏ guard.

Trong transaction lỗi: rollback. Sau COMMIT không có automatic downgrade; preserve subsequent writes rồi chọn reviewed compensation hoặc restore backup vào database mới để so/recover. Không restore đè HotelOpsDB, không WITH REPLACE. Actual restore rehearsal chứng minh backup đọc/restore được, không chứng minh đã diễn tập cutover backend production.

## H. Notes / Blockers

Không có blocker đã xác định đối với phạm vi RBAC+personnel hiện tại. Các notes phải được DEV1 xem khi duyệt:

1. Department retirement còn bắt buộc riêng, không hoàn thành; dữ liệu nghiệp vụ legacy nguyên vẹn.
2. DataScope EF mapping/PermissionService cần task sau; không claim Authorization Core đầy đủ.
3. Chưa thực thi authenticated HTTP integration; cần smoke test có credential được chủ tài khoản cung cấp riêng ở bước được duyệt, không reset password để test.
4. Audit enhancement6 rows và before/after payload scope cần nằm trong approval của main candidate cuối.
5. Filesystem length của backup chưa đọc được do ACL; SQL size/VERIFYONLY và actual restore đã xác minh. Không thay ACL.
6. Main script binding/fingerprints cố ý strict, chỉ đúng baseline hiện tại; phải kiểm tra lại ngay trước03B. Chưa tạm dừng writers hoặc thay connection string ở Task03A.
7. Recovery model nguồn là SIMPLE; không có bảo đảm PITR/tail-log cho writes sau backup. Cần writer quiescence/checkpoint phù hợp, không restore backup cũ để mất dữ liệu mới.

## I–J. Git Status và Final Verdict

Chỉ tạo package Task03A; không sửa C#, Authentication, CurrentUserAccessor, EF model/mapping, configuration, frontend, Permission Matrix hoặc Release chuẩn. Không commit/push/merge/PR. Rehearsal clone và backup được giữ, không cleanup/drop tự động.

PowerShell parse, JSON validation và git diff checks được kiểm tra trước bàn giao. Console accessor14/14 Passed; build/solution test Not Run vì không có C# changes. Không giấu lỗi ACL; đã ghi cách resume/verification ở trên.

**READY WITH NOTES — dừng tại Task03A và chờ phê duyệt riêng Task03B cho đúng candidate SHA-256 cùng changeset trên. HotelOpsDB vẫn ở 11/47/182, chưa Upgrade; không có Dry Run ghi hoặc COMMIT Upgrade nào trên nguồn.**
