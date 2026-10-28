# Authorization matrix — catalog hiện tại và contract pending

Đây là review, không sửa Permission Matrix, catalog, role grants hoặc policy. Catalog source: scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql. Runtime còn phụ thuộc session/user/assignment/branch/grant đang hiệu lực; bảng không khẳng định mọi user mang role đều được phép.

## Endpoint gates

| Proposal | Đăng nhập | Permission | Global / Branch / Own |
|---|---|---|---|
| /api/branches | Bắt buộc | UC-013 read mapping chưa chốt; MASTER_DATA_MANAGE chỉ candidate | Global admin / scoped OM và inactive branch visibility còn pending; không trả tất cả branches bằng auth-only |
| /api/branches/{branchId}/departments | Bắt buộc | UC-013 read mapping chưa chốt | Branch check dự kiến; OWN không có semantics; không suy ra từ ManagerUserId |
| /api/room-types | Bắt buộc | Không có ROOM_TYPE_VIEW; MASTER_DATA_MANAGE candidate chưa phê duyệt | Global catalog hay branch usage phải chốt D2 trước |
| /api/rooms?branchId=... | Bắt buộc | ROOM_VIEW có thật | Branch check cho một active branch; Global-only sẽ từ chối các BRANCH grants hiện có; OWN deny |

Không có endpoint anonymous/public. Authentication-only không thay thế permission. Chưa mở rộng quyền lookup cho roles không được catalog cấp.

## Các grants liên quan hiện tại

| Role code | ROOM_VIEW | MASTER_DATA_MANAGE | Diễn giải |
|---|---|---|---|
| SYS_ADMIN | Không | ALL | Không có bypass ROOM_VIEW theo tên admin |
| OPERATIONS_MANAGER | BRANCH | Không | Đủ grant loại phòng trong branch hợp lệ; chưa đủ để tự mở toàn UC-013 |
| HK_SUPERVISOR | BRANCH | Không | Branch room visibility |
| HK_STAFF | BRANCH | Không | Catalog BRANCH; không tự đổi thành OWN theo draft restricted |
| MAINTENANCE_LEAD | BRANCH | Không | Không chỉnh module maintenance |
| MAINTENANCE_STAFF | BRANCH | Không | Catalog BRANCH, khác cách diễn đạt restricted trong draft |
| DEPT_MANAGER | BRANCH | Không | DH branch-wide, không department-only |
| RECEPTION | BRANCH | Không | Room read grant không phê duyệt lifecycle actions |
| STOREKEEPER | Không | Không | INVENTORY_VIEW/ASSET_VIEW không cấp ROOM_VIEW |
| OWNER | Không | Không | Chỉ DASHBOARD_VIEW/REPORT_VIEW ALL; không suy ra directory |
| STAFF | BRANCH nhưng role inactive | Không | Fail-closed do role inactive, không coi là role được phép hiện hành |

Vai trò Administrator trong SRS không tự ánh xạ thành quyền tác nghiệp của SYS_ADMIN. Draft §4.4 Hotel Master Data/View Directory rộng hơn catalog; không dùng để bổ sung grant. User có nhiều assignments có thể được phép qua một đường grant hợp lệ, không deny dựa trên một role name; cũng không ghép role của assignment này với branch của assignment khác.

## Evaluator và cách nối request an toàn

- PermissionService kiểm tra caller, session context, restricted flag; active/nondeleted user; assignment dates; active role; branch access chưa revoked, granted_at hợp lệ; active target branch.
- ALL với assignment không bound branch có thể qua Branch check cho active branch; ALL bound branch không trở thành Global hoặc xuyên branch.
- BRANCH cần assignment branch phù hợp. OWN cần resource resolver chứng minh ownership; branch context không đủ. Unknown scope/DEPARTMENT fail-closed.
- CleaningTaskResourceResolver hiện chỉ xử lý cleaning execution resource. Không dùng cleaning assignment làm room ownership; không thay/ghi đè resolver để mở ROOM_VIEW.
- PermissionBranchContext phải sẵn sàng trước policy evaluation. Controller query binding diễn ra sau middleware authorization; chỉ gắn Branch policy attribute mà không có context sẽ bị deny.
- Đề xuất không sửa pipeline: authenticated controller, validate request shape, gọi IAuthorizationService với current HttpContext.User và typed PermissionBranchContext qua existing Branch:ROOM_VIEW policy trước khi gọi service/query. Handler hiện hỗ trợ typed resource này. Đây là thiết kế tương lai, chưa thêm code.
- Service bắt buộc SQL predicate Room.BranchId = approvedBranchId; query param chỉ là selector, không là bằng chứng quyền. Không dựa vào client roles/branch claims thay cho server-side evaluation.
- Không chạy count/search/filter-ID lookups có dữ liệu trước authorization. Không trả branch names, row counts hoặc khác biệt lỗi giúp dò branch ngoài scope.
- Phiên hết hạn/revoked, user inactive/deleted/restricted, grant hết hạn/revoked hoặc branch inactive phải deny. No-store; không log credentials hay response chứa personnel.

Inactive branch visibility là quyết định riêng: current Branch evaluator deny kể cả ALL khi target branch inactive. Inactive rooms/departments bên trong active branch không đồng nghĩa inactive branch. Không tắt guard để làm filter hoạt động.

## Workflow consumers cần quyết định

Inventory/Material Request/Replenishment/Incident/Maintenance/Assets có thể cần selector cấu trúc nhưng nhu cầu UI không chứng minh quyền full directory. DEV1 chỉ đề xuất field contract; DEV2 giữ ownership các module này, đặc biệt Inventory Adjustment/Reversal. Phê duyệt riêng contextual lookup và phạm vi nếu muốn các roles thiếu ROOM_VIEW/master-read sử dụng.
