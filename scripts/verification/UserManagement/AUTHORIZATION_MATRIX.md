# Authorization matrix và security boundaries

Catalog source: scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql (62 permissions, không thêm mới). SRS §4.4 draft cho Administrator user CRUD/role configure/assign; Change Notes chưa duyệt matrix đầy đủ.

## Existing grants, không role bypass

- SYS_ADMIN hiện có USER_VIEW, USER_MANAGE, ROLE_MANAGE với ALL theo seed. Vẫn phải có active valid assignment, user/session hợp lệ và scope path; tên role không cấp quyền mặc định.
- DEPT_MANAGER có USER_VIEW/BRANCH; không có USER_MANAGE/ROLE_MANAGE trong catalog.
- OWNER chỉ dashboard/report ALL, không có ba quyền này.
- Các roles còn lại không có ba user-admin permissions theo seed hiện tại.
- Global USER_VIEW của UC141 fail-closed với BRANCH, OWN hoặc ALL branch-bound. Scoped directory visibility chưa triển khai; không mở DH vì chỉ thấy grant trong catalog.

| Operation | Actor SRS | Proposed gate | Scope/approval |
|---|---|---|---|
| Existing GET /api/users | Administrator | Global USER_VIEW đã có | Không thay đổi Task17 |
| Detail | Administrator qua UC141/005 | Global USER_VIEW candidate | O1 phê duyệt detail read slice; không mặc nhiên cho access-review fields |
| UC011 access review | Administrator | USER_VIEW vs ROLE_MANAGE pending | O1/O2; không tự OR hoặc gộp quyền read với assignment administration |
| UC004 create | Administrator | USER_MANAGE + role/scope authority cần chốt | O1; create selected roles không được thành escalation path |
| UC005 profile/reset | Administrator | USER_MANAGE | Global-only admin phase là proposal; target scope/security requirements vẫn áp dụng |
| UC006 status | Administrator | USER_MANAGE | Self/last-admin/open-work guards ngoài permission gate |
| UC007 branch/dept | Administrator | USER_MANAGE vs ROLE_MANAGE pending | Source and destination scopes đều cần thẩm quyền; O1/O4 |
| UC010 grant/revoke | Administrator | ROLE_MANAGE | Không tự grant quyền/branch vượt delegation authority; O5 |
| UC008/009 role/catalog writes | Administrator | ROLE_MANAGE | Deferred; fixed catalog/system role/last-admin conflict phải duyệt trước |

Global-only cho giai đoạn đầu là proposal giới hạn actor giống UC141, không diễn giải SRS đã quy định mọi admin endpoint phải Global. Không tự triển khai scoped user visibility bằng “target.PrimaryBranchId == caller branch”: user có thể đa branch; detail/full access report có thể lộ assignment ngoài reviewer scope. OWN không có user ownership resolver; self targetId không tạo permission. DH branch-wide không department-only.

## Runtime rules phải giữ

Cookie Authentication, SQL session validation, restricted-session middleware, CSRF mọi unsafe API methods và no-store. Không tin body callerId/role/branch như proof. Missing/expired/revoked session không được fallback. SQL unavailable không coi như user không tồn tại hoặc quyền được cấp.

Evaluator hiện caller-only: active/nondeleted user; role/assignment active/time-valid; branch-bound grant cần active branch + UserBranchAccess chưa revoked/granted hợp lệ. ALL null branch khác ALL branchbound; BRANCH cùng assignment branch; OWN phải có verified resource. Không mix branch từ assignment A và permission từ B. Access review không fake target principal.

Permission gate không đủ để grant role: O5 phải chốt delegation ceiling, protected/system roles, self-elevation và effective scope của global/branchbound assignments. Không tự đề ra rule “chỉ grant quyền caller đang có” là SRS rule; đó là một policy option chưa được chấp thuận. GBR027 chỉ xác nhận self-deactivate/self-revoke-admin bị cấm, không chứng minh mọi self-profile edit hoặc self-grant đều được phép/cấm.

## Session invalidation

| Change | Căn cứ và behavior cần giữ |
|---|---|
| Deactivate | UC006 revoke all target sessions trong cùng transaction |
| Reset credential | UC005 +023/024 revoke all; temp login restricted cho tới change password |
| Reactivate | Giữ lock hiện hữu; sessions revoked không phục hồi |
| Role revoke/grant, branch removal/transfer | UC007/009/010 yêu cầu quyền mới áp dụng next request. Evaluator đọc DB mỗi request; không nhất thiết logout-all theo SRS. Chốt O8 có bổ sung revoke-all cùng transaction cho thay đổi nhạy cảm hay không |
| Profile email/name/phone | SRS không bắt buộc revoke-all; không tự suy ra email là username hoặc credential |
| Role permissions UC009 | Next request áp dụng cho mọi assignees; hiện deferred, cần kiểm chứng cache/race trước implementation |

Revalidation phải tính đến permission của actor thay đổi giữa request gate và commit. Các write cần protocol kiểm tra lại quyền và invariant trong transaction; ongoing requests/commit race không được giải quyết chỉ bằng revoke cookie.

## Audit / data safety

Không trả raw credentials/session rows trong detail/access. Create/reset plaintext là one-time approved-flow response duy nhất, sau commit, không logs/audit/cache/GET-readback. Access review có personnel và grant topology, cần gate riêng; không trả arbitrary AuditLog JSON. Before/after whitelist bảo vệ credential fields. Session invalidation audit chỉ cần counts/metadata không tokens. Không dùng role IDs/user IDs fixture làm rules.
