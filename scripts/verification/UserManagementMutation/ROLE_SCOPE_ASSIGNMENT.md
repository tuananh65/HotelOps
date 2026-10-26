# Role / Branch / Department assignment readiness

## Model thực tế

| Thành phần | Biểu diễn được | Không tự bảo đảm |
|---|---|---|
| User.PrimaryBranchId / DepartmentId nullable | Một primary branch, một department của User | Không multi-department membership; không tự xác nhận dept thuộc primary branch |
| UserBranchAccess | Nhiều branches; unique(userId,branchId), AccessLevel,IsPrimary,GrantedAt/By,RevokedAt | AccessLevel không được PermissionService dùng làm authority; không departmentId hoặc full grant history |
| UserRole | Role+branch nullable+IsActive+AssignedAt/By+ExpiresAt; unique(userId,roleId,branchId) | Không revokedAt/By; không nhiều lịch sử lặp cùng tuple; branch-null semantics cần contract |
| RolePermission | Permission + DataScope; granted metadata | Không phải scope theo riêng user; client không được sửa DataScope qua assignment |
| Department | BranchId; optional ManagerUserId | Không là user-department membership table; ManagerUserId không định nghĩa DH authorization |

Unique tuple nghĩa revoke rồi insert lại cùng user/role/branch có thể vi phạm unique dù row inactive. Đề xuất reactivation/update row có audit before/after, không tạo duplicate history row; cần business approval về chronology/expiry. UserBranchAccess regrant cũng phải xử lý existing revoked tuple, không insert mù.

## Rules và trạng thái implementation

- SRS031 muốn nhiều branches và nhiều departments trong branch; hiện chỉ multi-branch access biểu diễn trực tiếp. Single primary department là **slice cần duyệt**, không tuyên bố full UC007.
- SRS004 yêu cầu active/existing referenced records trong actor scope; FK chỉ chứng minh existence, không active/same-branch/delegation.
- PermissionService kiểm active user/role/assignment/time; branch-bound grant cần active branch+active access. Chưa có assignment mutation service để enforce invariants khi ghi.
- Scope removal không tự xóa UserRole bound branch. Evaluator sẽ deny khi access revoked nhưng dữ liệu stale vẫn tồn tại; cần quyết định đồng thời inactive role rows hay retain-ineligible, audit rõ.
- BranchId null với ALL có thể là global privilege; không cho client tạo bằng default omission. BRANCH/OWN null branch không grant trong current evaluator.
- Primary branch ≠ toàn membership. Client targetId/branchId chỉ selectors, không proof.
- DH branch-wide theo Change Notes; không department-only. Canonical manifest OM một branch khác draft031 cho OM nhiều branches; cần reconcile trước writes.
- OM/DH cardinality/Storekeeper/SoD không suy ra hoàn chỉnh từ FK/index. Không copy historical cleanup choices hoặc IDs làm rules.

## Options

1. Single primary department + multi-branch access slice: đề xuất phase đầu nếu business xác nhận limit; no migration, cần source validation.
2. Full multi-department join: đáp ứng nhu cầu031 nhưng cần schema/DB-first/API approval riêng, không Task24.
3. Dùng ManagerUserId, AccessLevel hoặc JSON trong ScopeDescription để nhét memberships: không đúng relationships/evaluator, không đề xuất.

## Assignment workflow tối thiểu sau approval

UC007 targetACTIVE, branch/dept active/cùng scope; recheck open work trước removed scope và coordinate writers; synchronize primary flags/User primary fields/access rows theo một transaction, không hard-delete history. Không vừa transfer scope vừa revoke roles qua nhiều calls rồi hứa atomic.

UC010 targetACTIVE; active eligible role; validate expiry và branch authority; no self-admin revoke/last-admin loss/open executor work; grant/revoke+audit atomic. Use server actor/time, không nhận AssignedBy. Full replacement batch vs single grant/revoke endpoints cần chốt.

UC004 initial assignments cũng phải qua cùng approved validation, không bypass vì account mới. Catalog12 roles/62 permissions fixed; UC008/009 không thuộc task và không tự sửa role grants.

## Impacts / tests

Authentication không cần đổi cookie format; Authorization current grant reader cần giữ same-path eligibility. DEV2 cần phối hợp cross-branch eligibility và open-work guard, không sửa API của họ. Trial tests: duplicate/regrant tuples, primary/access mismatch, null branch privilege, inactive masters, expiry, crossbranch references, rollback tất cả rows/audit và concurrent transfer-vs-assign. Chưa chạy trong Task24.
