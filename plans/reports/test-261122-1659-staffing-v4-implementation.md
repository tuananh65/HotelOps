# Staffing V4 — implementation and verification

Yêu cầu hiện hành: attachment `82bc7a51-170d-4678-8cc5-509c7bd68b67/Pasted text.txt`. Thực hiện một agent, sử dụng AgentKit `ak-cook`. Đây là thay đổi dữ liệu trên môi trường kiểm thử nội bộ đã được Owner phê duyệt. Không sửa source ứng dụng, schema, permission policy, Git hoặc deploy.

DATABASE_TARGET: DESKTOP-JI8U50T\SQLEXPRESS / HotelOpsCompactDB.

BASELINE: Đọc lại trước apply:40 bảng,160 FK hợp lệ,54 users,15 ACTIVE,39 INACTIVE,10 role active,67 permissions,178 grants,57 user_roles,110 memberships,0 workday_assignments.54 định danh và role/scope khớp V3.

BACKUP_STATUS: PASS — COPY_ONLY/CHECKSUM, RESTORE VERIFYONLY; backup tiền kiểm đã restore thật vào HotelOpsCompactWorkdayTestStaffingV4202611221659. Backup ngay trước apply chính cũng VERIFYONLY thành công.

PREFLIGHT: PASS — đúng instance/database; không trùng employee_code/username/email; role/branch/department active; Admin có thể đăng nhập; Owner global12 grants; snapshot toàn bộ40 bảng bao gồm rowversion được đối chiếu lại dưới khóa trong transaction. Không có drift được chấp nhận ngầm.

EXISTING_USERS_PASSWORD_RESET:15/15 (IDs424–438).

V3_NEW_USERS_PASSWORD_SET:39/39 (IDs439–477).

V3_NEW_USERS_ACTIVATED:39/39.

DVCT_DEPARTMENT_HEAD_CREATED:YES — ID478, DH002, Nguyễn Quốc Minh, MinhNQ1, MinhNQ1@ducvuong.com, DEPT_MANAGER, DVCT, employment HK207.

DVCT_STOREKEEPER_CREATED:YES — ID479, EMP050, Trần Đức Khang, KhangTD1, KhangTD1@ducvuong.com, STOREKEEPER, DVCT/STORE210.

TOTAL_USERS:56.

ACTIVE_USERS:56.

INACTIVE_USERS:0.

PASSWORD_POLICY:Mật khẩu test chung theo phê duyệt; không đưa giá trị vào báo cáo. Dùng chính ASP.NET Identity PasswordHasher,210000 iterations,56 salt/hash độc lập; giữ nguyên API password policy hiện hữu. Đọc credential từ attachment vào bộ nhớ, truyền qua private pipe/SQL parameters; không ghi plaintext/hash/token vào script, log hay audit. Cả56 security stamps được đổi, lockout/counter được xóa;33 phiên chưa revoke tại thời điểm apply được thu hồi, lịch sử phiên giữ nguyên.

FORCE_PASSWORD_CHANGE:NO — temporary_password_expires_at=NULL cho56 users, không thay đổi authentication contract.

ADMIN_LOGIN:PASS — NamNH1 trước/sau apply; phiên cũ HTTP401; SYS_ADMIN global5 grants giữ nguyên.

OWNER_SCOPE:PASS trên fixture và primary; global OWNER,12 grants ALL, chỉ đọc, cả hai chi nhánh.

DVCT_SHIFT_MANAGE:PASS trên fixture và primary — MinhNQ1 quản lý lịch cả4 bộ phận DVCT, không quản lý DVBV. Employment HK chọn theo mẫu DH001 hiện hữu; không giới hạn SHIFT_MANAGE vào HK, không thêm authority membership hoặc department manager designation.

DVCT_STOREKEEPER_SCOPE:PASS trên fixture và primary — KhangTD1 đúng16 grants của STOREKEEPER, chỉ DVCT; nhập kho và kiểm kê chạy thật trên fixture, primary chỉ đọc các loại chứng từ và chặn DVBV.

AUTHENTICATION_TESTS:Fixture56/56 PASS; primary56/56 PASS; session/token format, expiry và không bắt đổi mật khẩu đều được kiểm tra thật.

RBAC_TESTS:PASS — đối chiếu exact effective grants/scope từng user cả fixture/primary; Admin/Owner write denied trên fixture; primary chỉ auth và đọc.

BRANCH_SCOPE_TESTS:PASS trên fixture và primary — mọi user có role branch đọc đúng branch, branch còn lại HTTP403; global Admin/Owner được kiểm tra riêng.

WORKDAY_TESTS:PASS trên fixture —30 danh sách ứng viên đúng role/branch/department; inactive bị loại và không đăng nhập; stale409; vượt capacity409; overlap409; khác branch403; Start tương lai bị chặn; Start/End của bản thân trong ngày; không hai ca WORKING; ghi timestamp thực; End stale bị chặn.4 assignment thử nghiệm chỉ nằm trên fixture:2 CANCELLED,2 ENDED. Primary chưa tạo lịch.

BACKEND_BUILD:PASS — dotnet build HotelOps.slnx --no-restore,0 warning/0 error.

FRONTEND_BUILD:PASS — npm run build.

INTEGRATION_TESTS:Fixture API68 PASS/0 FAIL; primary API61 PASS/0 FAIL; browser UI7 PASS/0 FAIL. dotnet test:26 PASS/0 FAIL/1 SKIP; frontend unit4 PASS. WorkerIntegrationTests bị skip vì yêu cầu fixture QA riêng và biến HOTELOPS_COMPACT_WORKER_TEST; không tuyên bố worker đã chạy trong lượt này.

AUDIT_BATCH:C7046EC2-244C-4EAA-8799-B9E946422659; primary audit249 A_CREATE,250 B_CREDENTIALS,251 C_ACTIVATE. Bắt đầu10:19:13.9478765Z, audit hoàn tất10:19:15.0693767Z (17:19 giờ Việt Nam), transaction commit10:19:15.104Z ngày22/11/2026. Kiểm tra batch ID trực tiếp từ SQL và validator, không dựa vào GUID bị che trong output formatter.

RECOVERY_STATUS:PASS — restored backup thực, rollback cuối batch SQL51799 giữ baseline54/15/39, snapshot cũ SQL51703 bị chặn, chạy lại trả ALREADY_APPLIED. Admin recovery từ credential trước reset đã diễn tập và đăng nhập thật trên fixture, sau đó khôi phục credential V4 với stamp mới và revoke session; không thực hiện recovery trên primary.

BLOCKERS:NONE trong phạm vi V4. Giới hạn kiểm thử:worker integration riêng chưa chạy, ghi rõ ở trên.

FINAL_VERDICT:IMPLEMENTED AND VERIFIED —56/56 tài khoản ACTIVE và đăng nhập thật thành công, giữ nguyên RBAC/schema/history;0 lịch ca được tạo trên primary. Không commit/push/merge/deploy.

## Backup và bằng chứng

- Restore-tested preflight backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261122_170730940.bak`.
- Primary immediately-before-apply backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261122_171912532.bak`.
- Metadata: `artifacts/staffing-v4/backup-restore-preflight.json`, `backup-HotelOpsCompactDB-APPLY.json`.
- State: `artifacts/staffing-v4/validation-HotelOpsCompactDB-before.json`, `validation-HotelOpsCompactDB-after.json`.
- Session/recovery: `artifacts/staffing-v4/session-transition-HotelOpsCompactDB.json` và file tương ứng fixture.
- API: `artifacts/staffing-v4/api-5786-results.json`; primary `api-5787-results.json`.
- UI: `artifacts/staffing-v4/ui-results.json`, `ui-admin.png`, `ui-admin-scrolled.png`, `ui-workday.png`, `ui-owner.png`, `ui-storekeeper.png`. Đã mở ảnh kiểm tra lịch DVCT và kho; header list vẫn giữ vị trí khi scroll.
- Environment: `artifacts/staffing-v4/environment-evidence.json`, `sql-client-evidence.json`. HotelOps API/FE loopback; V4 API Development/worker off. Tunnel hiện có trỏ gateway5080, không trỏ HotelOps, `/api/v2/auth/me` tại gateway404. SQL TCP1433 có bind mọi interface; các client SQL quan sát được là shared-memory/local; ba firewall profiles bật. Kết luận môi trường dựa trên cấu hình/tiến trình được quan sát và khai báo môi trường test của Owner, không phải kiểm định toàn bộ hạ tầng mạng.

## Bảo toàn và giới hạn

35 bảng nghiệp vụ/reference không thuộc phạm vi mutation được khóa và đối chiếu digest server-side trước/sau; không thay đổi. Giữ nguyên54 định danh,57 role assignments cũ (gồm3 lịch sử revoked),110 memberships cũ, audit cũ và lịch sử nghiệp vụ. Sau V4 có59 role assignments và114 memberships. Không thay đổi67 permission definitions,178 mappings hay40 bảng/160 FK. Không dùng Project1/Rebuild.

Role DEPT_MANAGER đủ SHIFT_MANAGE theo branch. V4 không quyết định quyền DEPARTMENT_AUTHORITY phê duyệt yêu cầu vật tư; các membership425/433 giữ nguyên như V3. Không tự bổ sung mapping phê duyệt ngoài yêu cầu.

Tên kho cũ “Kho Đà Nẵng” vẫn xuất hiện trong DVCT; đây là nhãn dữ liệu tồn tại trước V4, không phải truy cập sai branch. Không đổi master data ngoài staffing. Roster mới không bảo đảm tự động đủ nhân lực cho mọi ca/ngày nghỉ; chưa có tuần đích nên không xếp lịch chính.

Một session fixture cũ còn bản ghi chưa revoke sau test deactivate/reactivate; security stamp không còn khớp nên session không hợp lệ theo middleware. Batch V4 đã revoke toàn bộ phiên cũ; trường hợp này phát sinh sau batch do API status hiện hữu. Primary được kiểm tra riêng, yêu cầu0 stale-unrevoked sessions.

Lỗi công cụ lúc chuẩn bị đã sửa trước primary: Windows PowerShell5.1 cần UTF-8 BOM cho tên tiếng Việt; SQL không cho initializer tham chiếu biến cùng DECLARE; PowerShell ConvertFrom-Json trả mảng cần đếm trên biến; LASTEXITCODE không dùng để đánh giá script PowerShell nội bộ. Không lỗi nào làm partial commit; fault rehearsal đã rollback.

Kiểm tra file/log đang mở cần FileShare.ReadWrite; lượt đọc đầu có4 file bị khóa, đã chạy lại với chế độ sharing và ErrorActionPreference=Stop,37/37 file đọc thành công,0 literal credential. SQL audit V4 không có plaintext, hash hoặc accessToken. Bằng chứng: `artifacts/staffing-v4/secret-hygiene.json`.

Kiểm tra cuối primary10:24:03Z:56 ACTIVE,0 INACTIVE,0 invalid role/branch/FK,0 stale-unrevoked sessions,0 workday_assignments. Các9 tiến trình API/Vite và tiến trình con do V4 khởi tạo đã dừng; cổng5186/5786/5787 không còn listener, dịch vụ khác giữ nguyên. Nhật ký AgentKit: `plans/journals/2026-11-22-staffing-v4-account-activation-and-dvct-handoff.md`; AgentWiki publish skipped.

## File tạo trong lượt V4

- `scripts/database/compact-v2/10_StaffingV4.sql`: transaction3 stage, concurrency/scope/invariants/audit/idempotency.
- `scripts/database/compact-v2/Invoke-StaffingV4.ps1`: nguồn approval, tạo hash trong memory, backup, snapshot, thực thi.
- `scripts/database/compact-v2/StaffingV4PasswordHasher.cs`: helper dùng thư viện Identity thực, build độc lập dưới artifacts, không thêm project vào solution/API.
- `scripts/database/compact-v2/Verify-StaffingV4.ps1`: baseline/final roster, grants, FK, scope validator.
- `scripts/database/compact-v2/Test-StaffingV4SessionTransition.ps1`: apply có kiểm chứng session và fixture-only Admin recovery.
- `scripts/database/compact-v2/test-staffing-v4-api.mjs`:56 login/grant checks, branch, Workday và workflow tests; guard fixture port cho business writes.
- `plans/261122-1659-staffing-v4/plan.md`, báo cáo này và journal local; `artifacts/staffing-v4/` chứa helper build, script UI và bằng chứng đã loại secrets.

## Khi cần khôi phục

Giữ backup trước apply và audit249–251. Restore backup sang tên database cô lập mới trước, VERIFYONLY/CHECKCONSTRAINTS và đối chiếu54 user/access. Nếu chỉ cần Admin, lấy credential pre-reset trong bộ nhớ từ bản restore, cập nhật riêng Admin dưới transaction/rowversion guard, stamp mới, revoke session, giữ identity/SYS_ADMIN, audit không chứa credential, đăng nhập xác minh. Việc recovery thật là thao tác vận hành riêng cần phạm vi rõ; không chạy tự động từ báo cáo này. Không khôi phục toàn primary đè dữ liệu mới hoặc phục hồi session cũ một cách tự động.
