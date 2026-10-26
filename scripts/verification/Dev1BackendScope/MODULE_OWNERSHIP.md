# Module ownership và ranh giới phối hợp

## Bằng chứng phân công

AGENTS.md quy định BE1/DEV1 sở hữu FE-01/02/03/04/05/11/12/13; BE2/DEV2 sở hữu FE-06/07/08/09/10/14/15. Task 16 xác nhận DEV2 tiếp tục tích hợp auth vào sáu nhóm API đang có. Ownership là theo feature, không theo role gọi API: DH dùng Material Request không khiến module đó thuộc DEV1.

| Phạm vi | Owner có bằng chứng | DEV1 có thể làm | Điều kiện phối hợp |
|---|---|---|---|
| User/employee account; role/branch assignment, UC-004–011/141 | DEV1 FE-01 | API mới trong single project sau task riêng | Không có Employee entity riêng; dùng User.EmployeeCode và employment attributes. Không tự tạo HR/payroll module |
| Branch, floors/areas, departments, room types/rooms, UC-012–026/142 | DEV1 FE-02 | Reads trước; CRUD khi rules rõ | Deactivation phải kiểm tra tham chiếu/việc mở trong bảng DEV2; không sửa service DEV2 để né guard |
| Room readiness, UC-027 | DEV1 FE-03 | Phân tích authoritative status/read projection | Phụ thuộc Cleaning/Inspection và Incident/RoomBlock DEV2; Reception occupancy design còn thiếu quyết định |
| Cleaning UC-028–038; Inspection UC-039–048 | DEV1 FE-04/05 | Triển khai sau khi transition/time/SoD được chốt | Cleaning resolver chỉ hỗ trợ execution IN_PROGRESS; adapters mới/dispatcher là shared change |
| Operational tasks UC-102–113; work-time UC-111/138–140 | DEV1 FE-11/12 | Reads và workflow khi schema/time/handover rõ | Actors có MT/Storekeeper không chuyển ownership; không thay Maintenance/Inventory để tự đồng bộ intervals |
| Internal requests UC-114–123 | DEV1 FE-13 | Request workflow theo scope/assignment | UC-118 liên kết Incident/Material Request là interface phối hợp với DEV2; không sửa trực tiếp module của họ |
| Work categories/reference catalogs UC-063/064 | Shared FE-06 + FE-11; chưa có owner triển khai độc quyền | Thiết kế contract cùng DEV2 | Không mặc định giao toàn bộ cho DEV1; reference/SLA/check constraints và consumers dùng chung |
| Incident/Maintenance incl. preventive maintenance UC-049–062/143/144 | DEV2 FE-06 | Ghi nhận gaps và cung cấp auth contract | Không sửa controller/service/DTO/workflow, kể cả gap hiện chưa có endpoint |
| Assets UC-065–074 | DEV2 FE-07 | Đọc dependencies | Không code trùng asset/handovers vì actor là DH |
| Inventory UC-075–090 | DEV2 FE-08 | Ghi nhận blockers | Adjustment/Reversal UC-087/088 có code cũ nhưng contract mới đang do DEV2 xử lý; không coi đã hoàn tất hoặc sửa thay |
| Material Requests UC-091–099; Replenishment UC-100/101 | DEV2 FE-09/10 | Cung cấp authenticated identity/permission integration hướng dẫn | Approval fallback/SoD và cleaning/task linkage phải phối hợp; DEV1 không tự sửa approval rules |
| Notifications/dashboard UC-124–134; reports/audit UC-135–137 | DEV2 FE-14/15 | Xác định event/audit cần phát sinh từ workflow DEV1 | Viết audit cho own transaction không đồng nghĩa sở hữu API audit viewer/report |

## Shared components cần thống nhất trước sửa

- HotelOpsDbContext, generated Entities, schema/release scripts, Program.cs DI, authentication/cookie/session pipeline, permission policy/provider/handler, resource resolver dispatch và shared error contract.
- DI hiện chỉ đăng ký một `IPermissionResourceResolver` là CleaningTaskResourceResolver. Đăng ký thêm bằng cùng interface có thể ghi đè registration hiệu lực. Dispatcher đa module cần thiết kế/test fail-closed và approval shared change theo AGENTS.md.
- Quyền check resource phải lấy branch/ownership từ DB; `PermissionBranchContext` cần có trước authorization. Không coi branch/query/ActorUserId do client truyền là bằng chứng quyền.
- Role/grant/scope updates cần bảo toàn immediate revocation semantics; không cache permission cố định theo session. UC-011 target-user access review cần read model an toàn riêng: IPermissionService hiện evaluate **caller hiện tại**, không dùng giả caller hoặc truyền target UserId qua claims.
- `TemporaryCredentialService.ApplyAsync` sở hữu transaction và từ chối context có tracking/transaction. UC-004 yêu cầu user + roles + scope + credential + audit atomic; không bọc ApplyAsync vào transaction tạo user. Cần thiết kế phối hợp Auth trước coding create-account.
- Thiết kế transaction/revalidation của user deactivation, branch/department reassignment phải chống race với việc giao việc mới. Last-admin/self-admin/OM/DH/SoD guards là business logic, không được thay bằng permission check đơn thuần.

## Các xung đột đã xác định

| Nguồn | Sai khác | Cách xử lý trong kế hoạch |
|---|---|---|
| Draft matrix Hotel Master Data View Directory | Cho Owner xem; release OWNER chỉ DASHBOARD_VIEW/REPORT_VIEW | Không tự cấp ROOM_VIEW/MASTER_DATA_MANAGE cho Owner |
| Draft matrix Operational Task Execute | Tất cả actor đều No, nhưng UC-107–112 có executors và catalog TASK_EXECUTE | Chờ chốt actor/scope matrix; không suy permission từ diagram |
| Draft matrix / GBR-111 | DH theo department, approval flow cũ | Tôn trọng branch-wide DH clarification; MR approval thuộc DEV2, không sửa trong task DEV1 |
| GBR-029 | Nhắc chín system actor roles, khác catalog 12/10 active | Không mở API tạo role tùy ý làm thay canonical catalog |
| GBR-005 | Rowversion cho mọi update; nhiều entity DEV1 không có | Không invent ExpectedRowVersion hoặc giả token từ UpdatedAt; cần quyết định DB/concurrency riêng |
| GBR-138–144 | Work intervals, correction workflow, shift handover snapshots/acknowledgment | Không có model chuyên biệt; progress/event text không tự chứng minh đáp ứng đầy đủ |
| GBR-139 | Có tên `work_interval.correct` trong SRS | Không có code tương ứng trong catalog; đây là unresolved mapping, không tạo code mới |
| UC-027/Reception notes | Tính trạng thái từ nguồn, occupancy/release chưa đồng bộ tài liệu | Không triển khai arbitrary room-status endpoint hoặc transition suy đoán |

## Database readiness

Canonical trial đã xác minh 82 bảng/9 rowversions cho baseline. Chín rowversions tập trung Inventory roots, MaterialRequest và MaterialReplenishment; User/Role/Branch/Department/RoomType/Room/CleaningTask/OperationalTask/WorkShift không có RowVersion trong generated model. User không có employee table riêng; RoomType là catalog toàn hệ thống vì không có BranchId. Không đưa branch filter giả vào RoomType.

Có ShiftTemplate/WorkShift/ShiftAssignment/ShiftAttendance nhưng chưa có WorkInterval, interval correction hoặc shift-handover snapshot/ack entity. Có InternalRequestEvent/OperationalTaskProgress/Attachment, nhưng chưa có contract đủ để suy ra storage hoàn chỉnh cho task criteria, recurring definitions, acknowledged request handover và generic linked work. Cần thiết kế được phê duyệt thay vì tạo cột/JSON schema tùy ý.

Task 16 không kiểm tra SQL thực tế. Schema readiness dựa vào source và evidence Task 15; không suy ra HotelOpsDB chính đã được upgrade.
