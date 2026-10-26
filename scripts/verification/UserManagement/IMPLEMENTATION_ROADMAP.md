# Roadmap User Management

## Ưu tiên

| Thứ tự | Scope | Dependency / readiness |
|---|---|---|
| 1 | Read-only User Account Detail, slice UC141/005 | Có USER_VIEW và User model; chốt O1 minimal fields/global-only trước. Không gộp UC011 |
| 2 | Effective roles/access review UC011 | O1/O2: target read semantics và gate, conditional OWN, path-preserving scope |
| 3 | Concurrency/transaction/delegation design cho mutations | O3/O4/O5/O6/O7/O8 trước code; không thêm schema ở task review |
| 4 | Profile update UC005; credential reset alternative flow riêng | Profile stale check/audit; reset one-time response và transaction reuse approval |
| 5 | Create UC004 | Employee code, multi-dept/scope slice, selected-role authority và full atomic credential issuance |
| 6 | Status UC006; scope UC007; roles UC010 | Last-admin/shared open-work guards và race protocol, session invalidation đã chốt |
| 7 | UC008/009 nếu được giao riêng | Dynamic role vs fixed catalog và system-role policy; không tự đưa vào phase1 |

Việc ưu tiên detail trước mutation giảm dependency chứ không thay thế việc hoàn thiện toàn User Management. Không chuyển sang Hotel Structure/Housekeeping trong task này.

## First recommended coding task

**Read-only GET /api/users/{userId}, minimal User Account Detail** sau approval O1.

- Scope: positive targetId, User chưa DeletedAt, DTO id/employeeCode/username/fullName/email/phone/accountStatus/primary branch/department; no credential, full assignment graph hoặc effective permissions.
- Gate đề xuất: existing Global USER_VIEW, current validated cookie/session, no-store; BRANCH/OWN không được mở trong phase này. Không hardcode SYS_ADMIN/OWNER.
- Entities: User, Branch, Department; AsNoTracking/async/CancellationToken, không schema/write.
- API statuses:200 found,401 invalid session,403 thiếu permission,404 generic missing/deleted; validation400. Đây là thiết kế đề xuất phải duyệt trước.
- Không đặt version giả trên DTO để “đáp ứng” GBR005; mutation concurrency giải quyết O3 riêng.
- Tests: active global grant allow; branchbound ALL/BRANCH/OWN deny; no grant/admin-name-only/OWNER deny; restricted/revoked/expired session; deleted/missing/inactive target behavior; fields whitelist/no-store; positive ID boundaries và actual EF SQL projection trên isolated trial khi practical.
- Acceptance: giữ UC141 list contract/regressions; query không chứa hash/session/navigation entities; no target mutation; build+relevant tests pass; báo cáo SQL runtime limitations rõ.
- Nếu cần thêm scoped DI registration Program.cs, giải thích shared impact và xin approval riêng theo AGENTS; approval UC141 trước đây chỉ áp dụng service UC141.

## Database và shared dependencies

User.EmployeeCode unique/max40; username100/email150/fullName200/phone20. User không rowversion, employee riêng hoặc multi-department join. UserRole có branch nullable/active/assignedAt/expiresAt nhưng không revokedAt. UserBranchAccess unique(user,branch), primary/accessLevel/granted/revoked; không departmentId. RolePermission giữ DataScope; AuditLog old/new/changedFields có thể chứa secrets nếu không whitelist.

PermissionService/EfPermissionDataSource cung cấp caller evaluation, chưa target access report/delegation guards. SessionRevocationService yêu cầu caller SQL transaction, user-before-session lock và clean untracked context. TemporaryCredentialService sở hữu transaction riêng; không dùng nested call để giả atomic create.

Last-admin cần bảo vệ cả nhiều user/roles/grants cùng lúc, không chỉ lock target. Open-work guard phải phối hợp readonly contracts với DEV2 incidents/maintenance và related assignment writers; chưa tự chọn status “open” theo phỏng đoán. Không sửa module Inventory/Incident/Maintenance/Assets/MaterialRequests/Replenishments hoặc Adjustment/Reversal.

## Fixtures và verification cho implementation sau này

Chỉ trên DB trial/test độc lập đã xác minh an toàn. Canonical reference seed tách personnel fixtures:

- Hai active branches và canonical departments; active/inactive branch/dept fixtures.
- Hai admin-capable users để test last-admin/self/concurrent cross-revocation; user không quyền/OWNER/DH scoped; multi-role/path grants và expiry/revoked memberships.
- Active/inactive/deleted/locked/temp account cases; duplicate username/email/employeeCode và SQL collation behavior.
- Session fixtures restricted/expired/revoked, reset-vs-login/touch/change races.
- Open assigned work từng module có authoritative states được duyệt; kiểm tra handover/removal race, không tạo fixture bằng ghi HotelOpsDB.
- Read tests không cần credential reset hoặc business writes trên DB chính. Write workflow tests phải chứng minh rollback account+scope+roles+credential+sessions+audit; secret không log; stale-client updates và two-admin race; commit-unknown không retry mù.

## Verification Task21

Documentation-only: build/tests/API/DB runtime Not Run; chưa có endpoint mới. Kiểm tra source inventory, git diff --check và cached check, whitespace sáu file, hash các files trước task. Không chạy SQL/migrations hoặc mở HotelOpsDB. Không lấy kết quả144/167 của task cũ làm verification các chức năng mới.

Kết quả: git diff --check và git diff --cached --check PASS; tracked tree/index sạch. Sáu file mới không trailing whitespace/dòng trống thừa cuối file. SHA-256 của565 file có trước task giữ nguyên, bao gồm toàn bộ Task20A và local artifacts; chỉ thêm đúng sáu Markdown. Untracked tăng128 lên134; không staging hoặc commit.
