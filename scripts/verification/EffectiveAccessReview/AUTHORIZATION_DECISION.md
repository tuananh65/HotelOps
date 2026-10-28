# Authorization Contract Gate — FAIL / thiếu quyết định

## Bằng chứng

| Nguồn | Chứng minh được | Không chứng minh được |
|---|---|---|
| SRS02 §1.11 UC-011 (khoảng dòng899–974) | Actor Administrator; logged-in + permission theo GBR001/002; xem roles/branches/departments/permissions theo feature; filter feature/permission; biết role cấp grant; read-only | Mã permission API, visibility cross-branch/self, exact field whitelist |
| SRS01 §4.4 Permission Matrix | User CRUD và role Configure/Assign dành Administrator trong draft | Không có approved row/code map Access Review sang USER_VIEW/USER_MANAGE/ROLE_MANAGE; matrix được đánh dấu NOT APPROVED |
| RBAC Catalog Seed03 dòng39–41,105–107,188 | Ba permission đều tồn tại; SYS_ADMIN được cấp ALL của cả ba; DEPT_MANAGER có USER_VIEW/BRANCH | Có cùng roles được cấp nhiều permission không làm các permission tương đương; tên manage không chứng minh quyền UC011 |
| Task16 coverage UC011 | Mapping USER_VIEW / ROLE_MANAGE chưa xác nhận; caller evaluator không phải target reviewer | Không có approval bổ sung |
| Task21 AUTHORIZATION_MATRIX + OUTSTANDING_DECISIONS O1/O2 | UC011 gate và report visibility vẫn pending | Không thể coi API proposal trong Task21 là approval |
| Task22 explicit scope và README | Global USER_VIEW được duyệt cho minimal detail-only | Không phê duyệt UC011/effective permissions, không mở scoped visibility |

## Kết luận từng gate

- Actor: Administrator theo SRS; không hardcode role name thành authority.
- Permission: **UNRESOLVED**. USER_VIEW, USER_MANAGE, ROLE_MANAGE hoặc tổ hợp không được tự chọn, kể cả chọn quyền có vẻ chặt hơn.
- Cross-branch: **UNRESOLVED** cho access report. Global detail approval không chuyển giao quyền xem toàn grant topology.
- Self-access: **UNRESOLVED**; targetId==callerId không tạo quyền. Không mở /me shortcut.
- Data visibility: **UNRESOLVED** cho full memberships, eligible/ineligible paths và target inactive account.
- Multiple assignments: model hỗ trợ; phải giữ từng path, không trộn branch và role khác nhau, không trả global boolean cho resource-dependent rules.

Task23 chỉ cho implementation khi đủ căn cứ; vì permission/visibility thiếu nên không tạo endpoint. Đây là blocker contract, không phải blocker DB connectivity hay coding difficulty.

## Tùy chọn cần developer phê duyệt rõ

Có thể chọn một existing permission, hoặc xác nhận yêu cầu tổ hợp, kèm scope. Global-only phase có thể giữ điều kiện unbound ALL như detail nếu được duyệt riêng; chưa có quyết định đó trong task hiện tại. Không tự OR USER_VIEW/ROLE_MANAGE, không tự AND để “an toàn hơn”.

Catalog SYS_ADMIN có grant không phải bypass; OWNER dashboard/report không tạo quyền access review. USER_VIEW/BRANCH của DH không tự cấp report cross-branch. Unknown scope và OWN thiếu resource vẫn fail-closed.
